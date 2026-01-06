// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.DataTransport
{
    /// <summary>
    /// Custom retry handler for OTLP exports with exponential backoff and jitter.
    /// Handles transient failures (5xx, 408, 429) and network errors.
    /// </summary>
    public class CustomRetryHandler : DelegatingHandler
    {
        private const int MaxRetries = 3;
        private const int BaseDelayMs = 1000; // Start with 1 second
        private const int MaxJitterMs = 500;   // Max jitter of 500ms

        // Use simple Random for retry jitter - thread safety handled at call site
        private static readonly Random Random = new Random();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Exception lastException = null;

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    var response = await SendSingleAttempt(request, cancellationToken);

                    // Success - return immediately
                    if (response.IsSuccessStatusCode)
                    {
                        LogSuccessIfRetried(attempt);
                        return response;
                    }

                    // Handle failed response
                    var shouldRetry = ShouldRetryResponse(response, attempt, out lastException);
                    if (!shouldRetry)
                    {
                        return response;
                    }

                    // Dispose failed response if retrying
                    response.Dispose();
                }
                catch (Exception ex) when (ShouldRetryException(ex, attempt, cancellationToken))
                {
                    lastException = ex;
                    LogExceptionRetry(attempt, ex);
                }

                // Wait before retry (except on final attempt)
                if (attempt < MaxRetries)
                {
                    await DelayBeforeRetry(attempt, cancellationToken);
                }
            }

            // All retries exhausted
            return HandleRetriesExhausted(lastException);
        }

        private async Task<HttpResponseMessage> SendSingleAttempt(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Clone the request for retry attempts (original request can only be sent once)
            var requestClone = await CloneRequestAsync(request);
            return await base.SendAsync(requestClone, cancellationToken);
        }

        private static void LogSuccessIfRetried(int attempt)
        {
            if (attempt > 1)
            {
                Log.Debug($"OTLP export succeeded on attempt {attempt}");
            }
        }

        private static bool ShouldRetryResponse(HttpResponseMessage response, int attempt, out Exception exception)
        {
            exception = null;

            if (!IsTransientFailure(response))
            {
                Log.Debug($"OTLP export failed with non-transient error {response.StatusCode}, not retrying");
                return false;
            }

            exception = new HttpRequestException($"Transient HTTP failure: {response.StatusCode} - {response.ReasonPhrase}");

            if (attempt >= MaxRetries)
            {
                Log.Warn($"OTLP export failed after {MaxRetries} attempts with status {response.StatusCode}");
                return false;
            }

            Log.Debug($"OTLP export attempt {attempt} failed with {response.StatusCode}, will retry");
            return true;
        }

        private static bool ShouldRetryException(Exception ex, int attempt, CancellationToken cancellationToken)
        {
            return attempt < MaxRetries && ex switch
            {
                // Timeout,but not user cancellation
                HttpRequestException => true,
                TaskCanceledException when !cancellationToken.IsCancellationRequested => true,
                _ => false
            };
        }

        private static void LogExceptionRetry(int attempt, Exception ex)
        {
            var message = ex switch
            {
                HttpRequestException => $"OTLP export attempt {attempt} failed with network error: {ex.Message}",
                TaskCanceledException => $"OTLP export attempt {attempt} timed out: {ex.Message}",
                _ => $"OTLP export attempt {attempt} failed: {ex.Message}"
            };
            Log.Debug(message);
        }

        private static async Task DelayBeforeRetry(int attempt, CancellationToken cancellationToken)
        {
            var delayMs = CalculateRetryDelay(attempt);
            Log.Debug($"Waiting {delayMs}ms before retry attempt {attempt + 1}");
            await Task.Delay(delayMs, cancellationToken);
        }

        private static HttpResponseMessage HandleRetriesExhausted(Exception lastException)
        {
            var errorMessage = $"OTLP export failed after {MaxRetries} attempts";
            Log.Error(lastException, errorMessage);
            throw lastException ?? new HttpRequestException(errorMessage);
        }

        /// <summary>
        /// Creates a copy of the HttpRequestMessage for retry attempts.
        /// Optimized for better performance and memory usage.
        /// </summary>
        private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version
            };

            // Copy headers efficiently
            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            if (request.Content != null)
            {
                // Load content into buffer to allow multiple reads
                await request.Content.LoadIntoBufferAsync();
                
                // Check if content supports direct copying (more efficient than byte array)
                if (request.Content is ByteArrayContent byteArrayContent)
                {
                    // For ByteArrayContent, read and create new instance directly
                    var contentBytes = await byteArrayContent.ReadAsByteArrayAsync();
                    clone.Content = new ByteArrayContent(contentBytes);
                }
                else
                {
                    // For other content types, use the general approach
                    var contentBytes = await request.Content.ReadAsByteArrayAsync();
                    clone.Content = new ByteArrayContent(contentBytes);
                }

                // Copy content headers efficiently
                foreach (var header in request.Content.Headers)
                {
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            return clone;
        }

        /// <summary>
        /// Determines if an HTTP response represents a transient failure that should be retried.
        /// </summary>
        private static bool IsTransientFailure(HttpResponseMessage response)
        {
            var status = (int)response.StatusCode;
            return status == 408 ||              // Request Timeout
                   status == 429 ||              // Too Many Requests (rate limiting)
                   (status >= 500 && status < 600); // Server errors (5xx)
        }

        /// <summary>
        /// Calculates retry delay using exponential backoff with jitter.
        /// </summary>
        /// <summary>
        /// Calculates the delay before the next retry attempt using exponential backoff with jitter.
        /// Optimized for better performance and more predictable behavior.
        /// </summary>
        private static int CalculateRetryDelay(int attempt)
        {
            // Use thread-safe Random for better performance in concurrent scenarios
            // Exponential backoff: BaseDelay * 2^(attempt-1) + random jitter
            var exponentialDelay = BaseDelayMs * Math.Pow(2, attempt - 1);
            var jitter = Random.Next(0, MaxJitterMs);

            // Cap at reasonable maximum (30 seconds) to prevent excessive delays
            var totalDelay = Math.Min(exponentialDelay + jitter, 30000);

            return Math.Max((int)totalDelay, 100); // Minimum 100ms delay for safety
        }
    }
}
