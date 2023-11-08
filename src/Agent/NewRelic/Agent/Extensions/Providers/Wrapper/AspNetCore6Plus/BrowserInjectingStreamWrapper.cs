// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.AspNetCore6Plus
{
    /// <summary>
    /// Stream wrapper that handles injecting the browser script as appropriate
    /// </summary>
    public class BrowserInjectingStreamWrapper : Stream
    {
        private readonly IAgent _agent;
        private Stream _baseStream;
        private HttpContext _context;
        private bool _isContentLengthSet;

        public BrowserInjectingStreamWrapper(IAgent agent, Stream baseStream, HttpContext context)
        {
            _agent = agent;
            _baseStream = baseStream;
            _context = context;

            CanWrite = true;
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            if (!_isContentLengthSet && IsHtmlResponse())
            {
                _context.Response.Headers.ContentLength = null;
                _isContentLengthSet = true;
            }

            return _baseStream.FlushAsync(cancellationToken);
        }

        public override void Flush() => _baseStream.Flush();

        public override int Read(byte[] buffer, int offset, int count) => _baseStream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);

        public override void SetLength(long value)
        {
            _baseStream.SetLength(value);

            IsHtmlResponse(forceReCheck: true);
        }

        public override void Write(ReadOnlySpan<byte> buffer) => _baseStream.Write(buffer);

        public override void WriteByte(byte value) => _baseStream.WriteByte(value);


        public override void Write(byte[] buffer, int offset, int count)
        {
            // pass through without modification if we're already in the middle of injecting
            // don't inject if the response isn't an HTML response
            if (!CurrentlyInjecting())
            {
                var responseContentType = _context.Response.ContentType;
                var requestPath = _context.Request.Path.Value;
                if (ShouldInject(responseContentType, requestPath) && IsHtmlResponse())
                {
                    // Set a flag on the context to indicate we're in the middle of injecting - prevents multiple recursions when response compression is in use
                    StartInjecting();
                    _agent.TryInjectBrowserScriptAsync(responseContentType, requestPath, buffer, _baseStream)
                        .GetAwaiter().GetResult();
                    FinishInjecting();

                    return;
                }
            }

            _baseStream?.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // pass through without modification if we're already in the middle of injecting
            // don't inject if the response isn't an HTML response
            if (!CurrentlyInjecting())
            {
                var responseContentType = _context.Response.ContentType;
                var requestPath = _context.Request.Path.Value;
                if (ShouldInject(responseContentType, requestPath) && IsHtmlResponse())
                {
                    // Set a flag on the context to indicate we're in the middle of injecting - prevents multiple recursions when response compression is in use
                    StartInjecting();
                    await _agent.TryInjectBrowserScriptAsync(_context.Response.ContentType, _context.Request.Path.Value,
                        buffer.ToArray(), _baseStream);
                    FinishInjecting();

                    return;
                }
            }

            if (_baseStream != null)
                await _baseStream.WriteAsync(buffer, cancellationToken);
        }

        /// <summary>
        /// Checks (via IBrowserPreReqChecker) whether we should inject the RUM script for this request.
        /// </summary>
        /// <param name="contentType"></param>
        /// <param name="requestPath"></param>
        /// <returns></returns>
        private bool ShouldInject(string contentType, string requestPath) => _agent.ShouldInjectBrowserScript(contentType, requestPath);

        private const string InjectingRUM = "InjectingRUM";

        private void FinishInjecting() => _context.Items.Remove(InjectingRUM);
        private void StartInjecting() => _context.Items.Add(InjectingRUM, null);
        private bool CurrentlyInjecting() => _context.Items.ContainsKey(InjectingRUM);

        public override async ValueTask DisposeAsync()
        {
            _context = null;

            await _baseStream.DisposeAsync();
            _baseStream = null;
        }

        public override bool CanRead { get; }
        public override bool CanSeek { get; }
        public override bool CanWrite { get; }
        public override long Length { get; }
        public override long Position { get; set; }

        private bool? _isHtmlResponse = null;

        private bool IsHtmlResponse(bool forceReCheck = false)
        {
            if (!forceReCheck && _isHtmlResponse.HasValue)
                return _isHtmlResponse.Value;

            // we need to check if the active request is still valid
            // this can fail if we're in the middle of an error response
            // or url rewrite in which case we can't intercept
            if (_context?.Response == null)
                return false;

            // Requirements for script injection:
            // * text/html response
            // * UTF-8 formatted (either explicitly or no charset defined)
            _isHtmlResponse =
                _context.Response.ContentType != null &&
                _context.Response.ContentType.Contains("text/html", StringComparison.OrdinalIgnoreCase) &&
                (_context.Response.ContentType.Contains("utf-8", StringComparison.OrdinalIgnoreCase) ||
                  !_context.Response.ContentType.Contains("charset=", StringComparison.OrdinalIgnoreCase));

            if (!_isHtmlResponse.Value)
            {
                _agent.CurrentTransaction?.LogFinest($"Skipping RUM injection: Not an HTML response. ContentType is {_context.Response.ContentType}");
                return false;
            }

            // Make sure we force dynamic content type since we're
            // rewriting the content - static content will set the header explicitly
            // and fail when it doesn't match if (_isHtmlResponse.Value)
            if (!_isContentLengthSet && _context.Response.ContentLength != null)
            {
                _context.Response.ContentLength = null;
                _isContentLengthSet = true;
            }

            return _isHtmlResponse.Value;
        }

    }
}
