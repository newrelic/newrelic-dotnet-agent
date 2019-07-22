using JetBrains.Annotations;
using NewRelic.Agent.Core.Utils;
using NewRelic.Core;
using NewRelic.Core.Logging;
using System;
using System.IO;
using System.Text;

namespace NewRelic.Agent.Core.BrowserMonitoring
{
	public class BrowserMonitoringStreamInjector : Stream
	{
		[NotNull]
		private readonly BrowserMonitoringWriter _jsWriter;

		[NotNull]
		private readonly Encoding _contentEncoding;

		[CanBeNull]
		private Action<Byte[], Int32, Int32> _streamWriter;

		public BrowserMonitoringStreamInjector([NotNull] Func<String> getJavascriptAgentScript, [NotNull] Stream output, [NotNull] Encoding contentEncoding)
		{
			_jsWriter = new BrowserMonitoringWriter(getJavascriptAgentScript);
			OutputStream = output;
			_contentEncoding = contentEncoding;
		}

		public override Boolean CanRead => OutputStream.CanRead;
		public override Boolean CanSeek => OutputStream.CanSeek;
		public override Boolean CanWrite => OutputStream.CanWrite;
		public override Int64 Length => OutputStream.Length;

		public override Int64 Position
		{
			get { return OutputStream.Position; }
			set { OutputStream.Position = value; }
		}

		public override void Close()
		{
			OutputStream.Close();
			base.Close();
		}

		[NotNull]
		private Stream OutputStream { get; }

		public override void Flush()
		{
			OutputStream.Flush();
		}

		public override Int64 Seek(Int64 offset, SeekOrigin origin)
		{
			return OutputStream.Seek(offset, origin);
		}

		public override void SetLength(Int64 value)
		{
			OutputStream.SetLength(value);
		}

		public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
		{
			return OutputStream.Read(buffer, offset, count);
		}

		public override void Write(Byte[] buffer, Int32 offset, Int32 count)
		{
			// BEWARE: There is no try/catch between this method and the users application!  Anything that can throw *must* be wrapped in a try/catch block!  We cannot wrap this in a try/catch block because we should not catch exceptions thrown by the underlying stream.

			// the first time Write is called, get the function that we will use to write
			if (_streamWriter == null)
				_streamWriter = GetStreamWriter();

			_streamWriter(buffer, offset, count);
		}

		[NotNull]
		private Action<Byte[], Int32, Int32> GetStreamWriter()
		{
			try
			{
				return GetInjectingStreamWriter(_contentEncoding);
			}
			catch (Exception exception)
			{
				// logged at debug level since the exception is likely caused by the user setting the content-type to something invalid or the wrapper provided functions failing
				try { Log.Debug(exception); } catch { }
				return PassThroughStreamWriter;
			}
		}

		private void PassThroughStreamWriter([NotNull] Byte[] buffer, Int32 offset, Int32 count)
		{
			OutputStream.Write(buffer, offset, count);
		}

		[NotNull]
		private Action<Byte[], Int32, Int32> GetInjectingStreamWriter([NotNull] Encoding contentEncoding)
		{
			return (buffer, offset, count) =>
			{
				var scriptInjected = false;
				var originalBuffer = buffer;
				var originalOffset = offset;
				var originalCount = count;
				var trimmedBuffer = new TrimmedEncodedBuffer(contentEncoding, buffer, offset, count);

				try
				{
					var injectedStreamBytes = TryGetInjectedBytes(contentEncoding, trimmedBuffer.Buffer, trimmedBuffer.Offset, trimmedBuffer.Length);
					if (injectedStreamBytes == null)
						return;

					scriptInjected = true;
					buffer = injectedStreamBytes;
					offset = 0;
					count = injectedStreamBytes.Length;

					// once we have written the JavaScript agent, switch over to the passthrough writer for the rest of the stream
					_streamWriter = PassThroughStreamWriter;
				}
				catch (Exception exception)
				{
					Log.Error($"Failed to inject JavaScript agent into response stream: {exception}");
					scriptInjected = false;
					buffer = originalBuffer;
					offset = originalOffset;
					count = originalCount;
					_streamWriter = PassThroughStreamWriter;
				}
				finally
				{
					// this needs to remain outside of the try block since we do not want to incorrectly catch exceptions thrown from the underlying filter
					if (scriptInjected && trimmedBuffer.HasLeadingExtraBytes)
						OutputStream.Write(trimmedBuffer.Buffer, trimmedBuffer.LeadingExtraBytesOffset, trimmedBuffer.LeadingExtraBytesCount);

					OutputStream.Write(buffer, offset, count);

					if (scriptInjected && trimmedBuffer.HasTrailingExtraBytes)
						OutputStream.Write(trimmedBuffer.Buffer, trimmedBuffer.TrailingExtraBytesOffset, trimmedBuffer.TrailingExtraBytesCount);
				}
			};
		}

		[CanBeNull]
		private Byte[] TryGetInjectedBytes([NotNull] Encoding contentEncoding, [NotNull] Byte[] buffer, Int32 offset, Int32 count)
		{
			var decoder = _contentEncoding.GetDecoder();
			var decodedBuffer = Strings.GetStringBufferFromBytes(decoder, buffer, offset, count);
			if (String.IsNullOrEmpty(decodedBuffer))
				return null;

			return TryGetBrowserMonitoringHeaders(contentEncoding, decodedBuffer);
		}

		[CanBeNull]
		private Byte[] TryGetBrowserMonitoringHeaders([NotNull] Encoding contentEncoding, [NotNull] String content)
		{
			var contentWithBrowserMonitoringHeaders = _jsWriter.WriteScriptHeaders(content);
			if (String.IsNullOrEmpty(contentWithBrowserMonitoringHeaders))
			{
				Log.Finest("RUM: Could not find a place to inject JS Agent.");
				return null;
			}

			Log.Finest("RUM: Injected JS Agent.");
			return contentEncoding.GetBytes(contentWithBrowserMonitoringHeaders);
		}
	}
}
