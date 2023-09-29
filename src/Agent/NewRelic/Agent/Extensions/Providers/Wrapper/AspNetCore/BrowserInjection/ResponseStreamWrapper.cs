// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace NewRelic.Providers.Wrapper.AspNetCore.BrowserInjection
{
    /// <summary>
    /// Wrapper for the response stream, handles checking for response content type and injecting the browser script if appropriate
    /// </summary>
    public class ResponseStreamWrapper : Stream
    {
        private Stream _baseStream;
        private HttpContext _context;

        private bool _isContentLengthSet = false;

        public ResponseStreamWrapper(Stream baseStream, HttpContext context)
        {
            _baseStream = baseStream;
            _context = context;

            CanWrite = true;
        }

        public override void Flush()
        {
            _baseStream.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            if (!_isContentLengthSet && IsHtmlResponse())
            {
                _context.Response.Headers.ContentLength = null;
                _isContentLengthSet = true;
            }

            return base.FlushAsync(cancellationToken);

        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _baseStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);

        public override void SetLength(long value)
        {
            _baseStream.SetLength(value);
            IsHtmlResponse(forceReCheck: true);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _baseStream.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            _baseStream.WriteByte(value);
        }


        public override void Write(byte[] buffer, int offset, int count)
        {
            if (IsHtmlResponse())
            {
                // inject browser script here 
                BrowserScriptInjectionHelper.InjectBrowserScriptAsync(buffer.AsMemory(offset, count), _context, _baseStream)
                    .GetAwaiter()
                    .GetResult();
            }
            else
            {
                _baseStream?.Write(buffer, offset, count);
            }
        }
        public override Task WriteAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (IsHtmlResponse())
            {
                // inject browser script here
                await BrowserScriptInjectionHelper.InjectBrowserScriptAsync(buffer, _context, _baseStream);
            }
            else
            {
                if (_baseStream != null)
                    await _baseStream.WriteAsync(buffer, cancellationToken);
            }
        }

        private bool? _isHtmlResponse = null;


        private bool IsHtmlResponse(bool forceReCheck = false)
        {
            if (!forceReCheck && _isHtmlResponse != null)
                return _isHtmlResponse.Value;

            // we need to check if the active request is still valid
            // this can fail if we're in the middle of an error response
            // or url rewrite in which case we can't intercept
            if (_context?.Response == null)
                return false;

            // Requirements for script injection:
            // * has to have result body
            // * 200 or 500 response
            // * text/html response
            // * UTF-8 formatted (explicit or no charset)

            _isHtmlResponse =
                _context.Response.StatusCode is 200 or 500 &&
                _context.Response.ContentType != null &&
                _context.Response.ContentType.Contains("text/html", StringComparison.OrdinalIgnoreCase) &&
                (_context.Response.ContentType.Contains("utf-8", StringComparison.OrdinalIgnoreCase) ||
                 !_context.Response.ContentType.Contains("charset=", StringComparison.OrdinalIgnoreCase));

            if (!_isHtmlResponse.Value)
                return false;

            // Make sure we force dynamic content type since we're
            // rewriting the content - static content will set the header explicitly
            // and fail when it doesn't match if (_isHtmlResponse.Value)
            if (!_isContentLengthSet && _context.Response.ContentLength != null)
            {
                _context.Response.Headers.ContentLength = null;
                _isContentLengthSet = true;
            }

            return _isHtmlResponse.Value;
        }

        protected override void Dispose(bool disposing)
        {
            //_baseStream?.Dispose();
            _baseStream = null;
            _context = null;

            base.Dispose(disposing);
        }

        #region Byte Helpers
        /// <summary>
        /// Tries to find a
        /// </summary>
        /// <param name="buffer">byte array to be searched</param>
        /// <param name="bufferToFind">byte to find</param>
        /// <returns></returns>
        public static int IndexOfByteArray(byte[] buffer, byte[] bufferToFind)
        {
            if (buffer.Length == 0 || bufferToFind.Length == 0)
                return -1;

            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == bufferToFind[0])
                {
                    bool innerMatch = true;
                    for (int j = 1; j < bufferToFind.Length; j++)
                    {
                        if (buffer[i + j] != bufferToFind[j])
                        {
                            innerMatch = false;
                            break;
                        }
                    }
                    if (innerMatch)
                        return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Returns an index into a byte array to find a string in the byte array.
        /// Exact match using the encoding provided or UTF-8 by default.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="stringToFind"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static int IndexOfByteArray(byte[] buffer, string stringToFind, Encoding encoding = null)
        {
            if (encoding == null)
                encoding = Encoding.UTF8;

            if (buffer.Length == 0 || string.IsNullOrEmpty(stringToFind))
                return -1;

            var bytes = encoding.GetBytes(stringToFind);

            return IndexOfByteArray(buffer, bytes);
        }
        #endregion


        public override bool CanRead { get; }
        public override bool CanSeek { get; }
        public override bool CanWrite { get; }
        public override long Length { get; }
        public override long Position { get; set; }
    }
}
