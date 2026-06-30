// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using Google.Protobuf;
using Grpc.Core;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.DataTransport;

/// <summary>
/// Hand-rolled unary gRPC transport over a raw <see cref="HttpClient"/> (no grpc-dotnet). The agent
/// compiles as netstandard2.0/net462, where Grpc.Net.Client cannot read the unary grpc-status trailer
/// (it reads a property bag only GrpcWebHandler fills) and net462 cannot read HTTP/2 trailers at all.
/// We frame the request ourselves, POST application/grpc over HTTP/2, and read the RecordStatus from
/// the response body - success is inferred from a parseable body, mirroring OTel's OtlpGrpcExportClient.
/// </summary>
public abstract class GrpcUnaryWrapper<TRequest, TResponse> : IGrpcUnaryWrapper<TRequest, TResponse>
    where TRequest : IMessage
    where TResponse : IMessage<TResponse>
{
    private static readonly PropertyInfo TrailingHeadersProperty = typeof(HttpResponseMessage).GetProperty("TrailingHeaders");

    private HttpClient _httpClient;
    private string _baseAddress;

    public bool IsConnected => _httpClient != null;

    /// <summary>The gRPC method path, e.g. "/com.newrelic.trace.v1.IngestService/RecordSpanBatchUnary".</summary>
    protected abstract string MethodPath { get; }

    /// <summary>Parser for the response message type.</summary>
    protected abstract MessageParser<TResponse> ResponseParser { get; }

    public bool CreateChannel(string host, int port, bool ssl, int connectTimeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            // Ensure any existing client is disposed before creating a new one.
            Shutdown();

            _httpClient = new HttpClient(CreateHttp2Handler(), disposeHandler: true);
            _baseAddress = $"{(ssl ? "https" : "http")}://{host}:{port}";

            // There is no way to validate an HTTP/2 connection without sending data, so creating the
            // client is treated as success (matches the streaming wrapper's behavior).
            return true;
        }
        catch (Exception ex)
        {
            throw new GrpcWrapperException("Unable to create new gRPC channel", ex);
        }
    }

    public bool TrySendData(TRequest item, Metadata headers, int sendTimeoutMs, CancellationToken cancellationToken, out TResponse response)
    {
        response = default;

        // The client can be torn down by a concurrent Shutdown/restart between the worker's IsConnected
        // check and this call. Treat "nothing to send on" as not-sent so the caller re-queues the items.
        var httpClient = _httpClient;
        if (cancellationToken.IsCancellationRequested || httpClient == null)
        {
            return false;
        }

        try
        {
            response = SendUnary(httpClient, item, headers, sendTimeoutMs, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            // Cancellation (shutdown/restart) or the send timeout elapsed. Not a gRPC-level error; re-queue.
            return false;
        }
        catch (GrpcWrapperException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GrpcWrapperException("Unable to send unary gRPC data", ex);
        }
    }

    private TResponse SendUnary(HttpClient httpClient, TRequest item, Metadata headers, int sendTimeoutMs, CancellationToken cancellationToken)
    {
        // Compress when the outgoing metadata declares gzip (grpc-encoding). grpc-dotnet did this implicitly
        // from the call options; the hand-rolled transport must gzip the frame and set the flag itself, and
        // the same grpc-encoding header is forwarded to the wire below so the flag and header stay consistent.
        var compress = WantsGzip(headers);
        var framed = GrpcUnaryFraming.Frame(item.ToByteArray(), compress);

        using (var request = new HttpRequestMessage(HttpMethod.Post, _baseAddress + MethodPath))
        {
            // HTTP/2 is required for gRPC. VersionPolicy (force-exact) isn't in the netstandard2.0 ref
            // assemblies, so we rely on Version 2.0 + TLS ALPN negotiation, which the trace observer supports.
            request.Version = new Version(2, 0);

            var content = new ByteArrayContent(framed);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
            request.Content = content;

            // TE: trailers is required by some gRPC servers or they abort the call.
            request.Headers.TryAddWithoutValidation("TE", "trailers");

            if (headers != null)
            {
                foreach (var entry in headers)
                {
                    if (!entry.IsBinary)
                    {
                        request.Headers.TryAddWithoutValidation(entry.Key, entry.Value);
                    }
                }
            }

            using (var sendCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                if (sendTimeoutMs > 0)
                {
                    sendCts.CancelAfter(sendTimeoutMs);
                }

                // ResponseContentRead: read the whole body so trailers are populated (where readable) and the
                // RecordStatus is available. Sync-over-async is safe here (no SynchronizationContext on the
                // agent's worker threads), matching the streaming wrapper's blocking pattern.
                using (var httpResponse = httpClient
                           .SendAsync(request, HttpCompletionOption.ResponseContentRead, sendCts.Token)
                           .ConfigureAwait(false).GetAwaiter().GetResult())
                {
                    return HandleResponse(httpResponse);
                }
            }
        }
    }

    private TResponse HandleResponse(HttpResponseMessage httpResponse)
    {
        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new GrpcWrapperException($"Unary gRPC endpoint returned HTTP {(int)httpResponse.StatusCode}", null);
        }

        var body = httpResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        var message = GrpcUnaryFraming.TryGetMessage(body);

        // Status ladder: trailers (.NET Core, via reflection) -> response headers -> body presence.
        // On .NET Framework the trailer is unreadable, so a parseable RecordStatus body is the success signal.
        var grpcStatus = TryReadGrpcStatus(httpResponse);
        if (grpcStatus.HasValue && grpcStatus.Value != (int)StatusCode.OK)
        {
            // Surface the status so UnaryDataService can map it (e.g. UNIMPLEMENTED -> shutdown). Core only.
            throw new GrpcWrapperException((StatusCode)grpcStatus.Value, $"Unary gRPC call returned grpc-status {grpcStatus.Value}", null);
        }

        if (message == null)
        {
            // No readable status and no message body: the server replied with a status-only/error response we
            // cannot interpret on this runtime. Treat as a generic failure so the caller re-queues.
            throw new GrpcWrapperException("Unary gRPC response contained no message", null);
        }

        return ResponseParser.ParseFrom(message);
    }

    private static bool WantsGzip(Metadata headers)
    {
        if (headers == null)
        {
            return false;
        }

        foreach (var entry in headers)
        {
            if (!entry.IsBinary
                && string.Equals(entry.Key, "grpc-encoding", StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.Value, "gzip", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int? TryReadGrpcStatus(HttpResponseMessage response)
    {
        // .NET Core: real HTTP/2 trailers. TrailingHeaders is absent from the netstandard2.0 reference
        // assemblies but present at runtime on .NET, so read it via reflection.
        if (TrailingHeadersProperty?.GetValue(response) is System.Net.Http.Headers.HttpHeaders trailers
            && TryGetIntHeader(trailers, "grpc-status", out var statusFromTrailers))
        {
            return statusFromTrailers;
        }

        // Fallback: some stacks/proxies surface the status in the response headers.
        if (TryGetIntHeader(response.Headers, "grpc-status", out var statusFromHeaders))
        {
            return statusFromHeaders;
        }

        return null; // .NET Framework: status unreadable; rely on body presence.
    }

    private static bool TryGetIntHeader(System.Net.Http.Headers.HttpHeaders headers, string name, out int value)
    {
        value = 0;
        if (headers != null && headers.TryGetValues(name, out var values))
        {
            foreach (var candidate in values)
            {
                if (int.TryParse(candidate, out value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static HttpMessageHandler CreateHttp2Handler()
    {
#if NETFRAMEWORK
        // .NET Framework's default HttpClientHandler is HTTP/1.1 only; WinHttpHandler provides HTTP/2 on
        // Windows 10+/Server 2016+ (the same handler grpc-dotnet itself requires on net462).
        return new WinHttpHandler();
#else
        // SocketsHttpHandler has full HTTP/2 + trailer support but isn't in the netstandard2.0 reference
        // assemblies, so build it via reflection at runtime on .NET 6+ (same pattern as NRHttpClient).
        if (System.Environment.Version.Major >= 6)
        {
            try
            {
                var assembly = Assembly.Load("System.Net.Http");
                var handlerType = assembly.GetType("System.Net.Http.SocketsHttpHandler");
                dynamic handler = Activator.CreateInstance(handlerType);
                handler.EnableMultipleHttp2Connections = true;
                return (HttpMessageHandler)handler;
            }
            catch (Exception ex)
            {
                Log.Info(ex, "Unable to create SocketsHttpHandler for the unary gRPC client; falling back to HttpClientHandler.");
            }
        }

        return new HttpClientHandler();
#endif
    }

    public void Shutdown()
    {
        var client = _httpClient;
        _httpClient = null;
        if (client == null)
        {
            return;
        }

        try
        {
            client.Dispose();
        }
        catch (Exception ex)
        {
            Log.Finest(ex, "{0}: Error encountered disposing the unary gRPC HttpClient", this.GetType().Name);
        }
    }
}
