// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using Grpc.Core;
using Grpc.Net.Client;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.DataTransport;

public abstract class GrpcUnaryWrapper<TRequest, TResponse> : IGrpcUnaryWrapper<TRequest, TResponse>
{
    private GrpcChannel _channel;

    public bool IsConnected => _channel != null;

    protected abstract TResponse SendDataImpl(GrpcChannel channel, TRequest item, Metadata headers, int sendTimeoutMs, CancellationToken cancellationToken);

    public bool CreateChannel(string host, int port, bool ssl, int connectTimeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            // Ensure old channel disposed before creating a new one
            Shutdown();

            var credentials = ssl ? new SslCredentials() : ChannelCredentials.Insecure;

            var uriBuilder = new UriBuilder
            {
                Scheme = ssl ? "https" : "http",
                Host = host,
                Port = port
            };

            var grpcChannelOptions = new GrpcChannelOptions
            {
                Credentials = credentials,
                HttpHandler = new UnaryTrailerBridgeHandler(CreateHttp2Handler()),
                DisposeHttpClient = true
            };

            _channel = GrpcChannel.ForAddress(uriBuilder.Uri, grpcChannelOptions);

            // In grpc-dotnet the only way to validate a connection is to send data over it,
            // which we do not want to do here, so creating the channel is treated as success.
            return true;
        }
        catch (Exception ex)
        {
            const string errorMessage = "Unable to create new gRPC Channel";

            var grpcEx = ex as RpcException ?? ex.InnerException as RpcException;
            if (grpcEx != null)
            {
                throw new GrpcWrapperException(grpcEx.StatusCode, errorMessage, grpcEx);
            }

            throw new GrpcWrapperException(errorMessage, ex);
        }
    }

    public bool TrySendData(TRequest item, Metadata headers, int sendTimeoutMs, CancellationToken cancellationToken, out TResponse response)
    {
        response = default;

        // The channel can be torn down by a concurrent Shutdown/restart between the worker's
        // IsConnected check and this call. Treat "nothing to send on" as not-sent (return false) so
        // the caller re-queues the items and lets the reconnect logic recover, rather than surfacing
        // it as a gRPC error.
        if (cancellationToken.IsCancellationRequested || !IsConnected)
        {
            return false;
        }

        try
        {
            response = SendDataImpl(_channel, item, headers, sendTimeoutMs, cancellationToken);
            return true;
        }
        catch (GrpcWrapperChannelNotAvailableException)
        {
            // Channel went away mid-send (e.g., a concurrent shutdown). Not a gRPC-level error.
            return false;
        }
        catch (Exception ex)
        {
            const string errorMessage = "Unable to send unary gRPC data";

            var grpcEx = ex as RpcException ?? ex.InnerException as RpcException;
            if (grpcEx != null)
            {
                throw new GrpcWrapperException(grpcEx.StatusCode, errorMessage, grpcEx);
            }

            throw new GrpcWrapperException(errorMessage, ex);
        }
    }

    // grpc-dotnet reads the unary grpc-status from HTTP/2 trailers. HttpClientHandler does not reliably
    // surface trailers, which produced "No grpc-status found on response" on otherwise-successful unary
    // calls (the streaming path tolerated it, unary did not). SocketsHttpHandler has correct HTTP/2 +
    // trailer support but isn't in the netstandard2.0 reference assemblies this project compiles against,
    // so it's created via reflection at runtime on .NET 6+ (same pattern as NRHttpClient.GetHttpHandler).
    private static HttpMessageHandler CreateHttp2Handler()
    {
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
                Log.Info(ex, "Unable to create SocketsHttpHandler for the unary gRPC channel; falling back to HttpClientHandler.");
            }
        }

        return new HttpClientHandler();
    }

    // Workaround for a grpc-dotnet (Grpc.Net.Client 2.80) unary trailer-timing issue: for unary responses
    // Bridges HTTP/2 response trailers into the location grpc-dotnet's netstandard2.0 build reads.
    //
    // Root cause: this assembly is compiled as netstandard2.0, so it uses grpc-dotnet's ns2.0 build.
    // That build's TrailingHeaders() helper does NOT read HttpResponseMessage.TrailingHeaders - it
    // reads a request property bag keyed "__ResponseTrailers" that only GrpcWebHandler populates. So
    // a plain HTTP/2 unary response (which carries grpc-status in real HTTP/2 trailers) is seen as
    // having no trailers -> Status(Cancelled, "No grpc-status found on response.") even though the
    // server returned grpc-status: 0. (Verified: the trailer IS present at runtime; the net6+ grpc
    // build reads it natively and works.)
    //
    // Workaround: drain the body (so the runtime populates the real trailers), then copy them into the
    // "__ResponseTrailers" bag the ns2.0 build reads. TrailingHeaders is absent from the ns2.0 ref
    // assemblies but present at runtime on .NET (Core), so it is accessed via reflection. Unary
    // responses are a single small RecordStatus, so buffering is cheap. Unary channel only; streaming
    // is unaffected. Does NOT help .NET Framework (no HTTP/2 trailers there - that path needs gRPC-Web).
    private sealed class UnaryTrailerBridgeHandler : DelegatingHandler
    {
        // Must match Grpc.Net.Client's internal TrailingHeadersHelpers.ResponseTrailersKey.
        private const string ResponseTrailersKey = "__ResponseTrailers";
        private static readonly PropertyInfo TrailingHeadersProperty =
            typeof(HttpResponseMessage).GetProperty("TrailingHeaders");

        public UnaryTrailerBridgeHandler(HttpMessageHandler inner) : base(inner) { }

        protected override async System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (TrailingHeadersProperty != null && response.Content != null && response.RequestMessage != null)
            {
                // Drain so the runtime surfaces the HTTP/2 trailing frame on TrailingHeaders.
                await response.Content.LoadIntoBufferAsync().ConfigureAwait(false);

                if (TrailingHeadersProperty.GetValue(response) is System.Net.Http.Headers.HttpHeaders trailingHeaders
                    && !response.RequestMessage.Properties.ContainsKey(ResponseTrailersKey))
                {
                    response.RequestMessage.Properties[ResponseTrailersKey] = trailingHeaders;
                }
            }

            return response;
        }
    }

    public void Shutdown()
    {
        if (_channel == null)
        {
            return;
        }

        try
        {
            _channel.ShutdownAsync().Wait();
        }
        catch (Exception ex)
        {
            Log.Finest(ex, "{0}: Error encountered shutting down gRPC channel", this.GetType().Name);
        }

        _channel = null;
    }
}
