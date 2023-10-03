// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Utilities;
using NewRelic.Core;
using NewRelic.Core.Logging;
using System;
using System.IO;
using System.Text;

namespace NewRelic.Agent.Core.BrowserMonitoring
{
    public class BrowserMonitoringStreamInjector : Stream
    {
        private readonly BrowserMonitoringWriter _jsWriter;

        private readonly Encoding _contentEncoding;

        private Action<byte[], int, int> _streamWriter;

        public BrowserMonitoringStreamInjector(Func<string> getJavascriptAgentScript, Stream output, Encoding contentEncoding, BrowserMonitoringWriter browserMonitoringWriter = null)
        {
            _jsWriter = browserMonitoringWriter ?? new BrowserMonitoringWriter(getJavascriptAgentScript);
            OutputStream = output;
            _contentEncoding = contentEncoding;
        }

        public override bool CanRead => OutputStream.CanRead;
        public override bool CanSeek => OutputStream.CanSeek;
        public override bool CanWrite => OutputStream.CanWrite;
        public override long Length => OutputStream.Length;

        public override long Position
        {
            get { return OutputStream.Position; }
            set { OutputStream.Position = value; }
        }

        public override void Close()
        {
            OutputStream.Close();
            base.Close();
        }

        private Stream OutputStream { get; }

        public override void Flush()
        {
            OutputStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return OutputStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            OutputStream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return OutputStream.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            // BEWARE: There is no try/catch between this method and the users application!  Anything that can throw *must* be wrapped in a try/catch block!  We cannot wrap this in a try/catch block because we should not catch exceptions thrown by the underlying stream.

            // the first time Write is called, get the function that we will use to write
            _streamWriter ??= GetInjectingStreamWriter(_contentEncoding);

            _streamWriter(buffer, offset, count);
        }

        private void PassThroughStreamWriter(byte[] buffer, int offset, int count)
        {
            OutputStream.Write(buffer, offset, count);
        }

        private Action<byte[], int, int> GetInjectingStreamWriter(Encoding contentEncoding)
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
                    Log.Error(exception, "Failed to inject JavaScript agent into response stream");
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

        private byte[] TryGetInjectedBytes(Encoding contentEncoding, byte[] buffer, int offset, int count)
        {
            var decoder = _contentEncoding.GetDecoder();
            var decodedBuffer = Strings.GetStringBufferFromBytes(decoder, buffer, offset, count);
            if (string.IsNullOrEmpty(decodedBuffer))
                return null;

            return TryGetBrowserMonitoringHeaders(contentEncoding, decodedBuffer);
        }

        private byte[] TryGetBrowserMonitoringHeaders(Encoding contentEncoding, string content)
        {
            var contentWithBrowserMonitoringHeaders = _jsWriter.WriteScriptHeaders(content);
            if (string.IsNullOrEmpty(contentWithBrowserMonitoringHeaders))
            {
                Log.Finest("RUM: Could not find a place to inject JS Agent.");
                return null;
            }

            Log.Finest("RUM: Injected JS Agent.");
            return contentEncoding.GetBytes(contentWithBrowserMonitoringHeaders);
        }
    }
}
