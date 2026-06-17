// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net.Http;
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
                HttpHandler = new HttpClientHandler(),
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
