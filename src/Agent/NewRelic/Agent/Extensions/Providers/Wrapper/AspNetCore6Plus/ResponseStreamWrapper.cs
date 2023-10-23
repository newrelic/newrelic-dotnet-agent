// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.AspNetCore6Plus
{
    /// <summary>
    /// Wrapper for the response stream, handles checking for response content type and injecting the browser script if appropriate
    /// </summary>
    public class ResponseStreamWrapper : Stream
    {
        private readonly IAgent _agent;
        private Stream _baseStream;
        private HttpContext _context;

        private bool _isContentLengthSet = false;

        public ResponseStreamWrapper(IAgent agent, Stream baseStream, HttpContext context)
        {
            _agent = agent;
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
                var curBuf = buffer.AsSpan(offset, count).ToArray();
                _agent.InjectBrowserScriptAsync(_context.Response.ContentType, _context.Request.Path.Value, curBuf, _baseStream)
                        .GetAwaiter()
                        .GetResult();
                return;
            }

            // fallback: just write the stream without modification
            _baseStream?.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (IsHtmlResponse())
            {
                await _agent.InjectBrowserScriptAsync(_context.Response.ContentType, _context.Request.Path.Value, buffer.ToArray(), _baseStream);
                return;
            }

            // fallback: just write the stream without modification
            if (_baseStream != null)
                await _baseStream.WriteAsync(buffer, cancellationToken);
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

        public override bool CanRead { get; }
        public override bool CanSeek { get; }
        public override bool CanWrite { get; }
        public override long Length { get; }
        public override long Position { get; set; }
    }
}
