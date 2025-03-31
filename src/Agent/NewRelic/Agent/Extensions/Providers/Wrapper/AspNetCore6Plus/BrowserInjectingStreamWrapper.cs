// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Logging;

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

        /// <summary>
        /// Flag gets set to true if we've captured an exception and need to disable browser injection
        /// </summary>
        public static bool Disabled { get; set; }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_context is { Response: not null } && !Disabled && !_isContentLengthSet && IsHtmlResponse())
            {
                if (!_context.Response.HasStarted)  // can't set headers if response has already started
                    _context.Response.ContentLength = null;
                _isContentLengthSet = true;
            }

            return _baseStream?.FlushAsync(cancellationToken) ?? Task.CompletedTask;
        }

        public override void Flush() => _baseStream?.Flush();

        public override int Read(byte[] buffer, int offset, int count) => _baseStream?.Read(buffer, offset, count) ?? 0;

        public override long Seek(long offset, SeekOrigin origin) => _baseStream?.Seek(offset, origin) ?? 0;

        public override void SetLength(long value)
        {
            _baseStream?.SetLength(value);

            if (!Disabled)
                IsHtmlResponse(forceReCheck: true);
        }

        public override void Write(ReadOnlySpan<byte> buffer) => _baseStream?.Write(buffer);

        public override void WriteByte(byte value) => _baseStream?.WriteByte(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            // pass through without modification if we're already in the middle of injecting
            // don't inject if the response isn't an HTML response
            if (_context != null && !Disabled && !CurrentlyInjecting() && IsHtmlResponse())
            {
                try
                {
                    // Set a flag on the context to indicate we're in the middle of injecting - prevents multiple recursions when response compression is in use
                    StartInjecting();
                    _agent.TryInjectBrowserScriptAsync(_context.Response?.ContentType, _context.Request?.Path, buffer, _baseStream)
                        .GetAwaiter().GetResult();
                }
                finally
                {
                    FinishInjecting();
                }

                return;
            }

            _baseStream?.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // pass through without modification if we're already in the middle of injecting
            // don't inject if the response isn't an HTML response
            if (_context != null & !Disabled && !CurrentlyInjecting() && IsHtmlResponse())
            {
                try
                {
                    // Set a flag on the context to indicate we're in the middle of injecting - prevents multiple recursions when response compression is in use
                    StartInjecting();
                    await _agent.TryInjectBrowserScriptAsync(_context.Response?.ContentType, _context.Request?.Path, buffer.ToArray(), _baseStream);
                }
                finally
                {
                    FinishInjecting();
                }

                return;
            }

            if (_baseStream != null)
                await _baseStream.WriteAsync(buffer, cancellationToken);
        }

        private const string InjectingRUM = "InjectingRUM";

        private void FinishInjecting() => _context?.Items.Remove(InjectingRUM);
        private void StartInjecting() => _context?.Items.Add(InjectingRUM, null);
        private bool CurrentlyInjecting() => _context?.Items.ContainsKey(InjectingRUM) ?? false;

        public override async ValueTask DisposeAsync()
        {
            _context = null;

            if (_baseStream != null)
            {
                await _baseStream.DisposeAsync();
                _baseStream = null;
            }
        }

        public override bool CanRead { get; }
        public override bool CanSeek { get; }
        public override bool CanWrite { get; }
        public override long Length { get; }
        public override long Position { get; set; }

        private bool? _isHtmlResponse = null;

        private bool IsHtmlResponse(bool forceReCheck = false)
        {
            try
            {
                if (!forceReCheck && _isHtmlResponse != null)
                    return _isHtmlResponse.Value;

                // we need to check if the active request is still valid
                // this can fail if we're in the middle of an error response
                // or url rewrite in which case we can't intercept
                if (_context?.Response == null)
                    return false;

                // Requirements for script injection:
                // * text/html response
                // * UTF-8 formatted (either explicitly or no charset defined)
                var responseContentType = _context.Response.ContentType;
                _isHtmlResponse =
                    responseContentType.Contains("text/html", StringComparison.OrdinalIgnoreCase) &&
                    (responseContentType.Contains("utf-8", StringComparison.OrdinalIgnoreCase) ||
                     !responseContentType.Contains("charset=", StringComparison.OrdinalIgnoreCase));

                if (!_isHtmlResponse.Value)
                {
                    _agent.CurrentTransaction?.LogFinest($"Skipping RUM injection: Not an HTML response. ContentType is {responseContentType}");
                    return false;
                }

                // Make sure we force dynamic content type since we're
                // rewriting the content - static content will set the header explicitly
                // and fail when it doesn't match if (_isHtmlResponse.Value)
                if (!_isContentLengthSet && _context.Response.ContentLength != null)
                {
                    if (!_context.Response.HasStarted) // can't set headers if response has already started
                        _context.Response.ContentLength = null;
                    _isContentLengthSet = true;
                }
            }
            catch (Exception e)
            {
                LogExceptionAndDisable(e);
            }

            return _isHtmlResponse ?? false;
        }

        private void LogExceptionAndDisable(Exception e)
        {
            _agent.Logger.Log(Level.Error,
                $"Unexpected exception. Browser injection will be disabled. Exception: {e.Message}: {e.StackTrace}");

            Disabled = true;
        }
    }
}
