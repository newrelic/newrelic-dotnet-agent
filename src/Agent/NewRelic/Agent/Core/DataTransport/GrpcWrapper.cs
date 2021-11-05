// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Grpc.Core;
using System.Threading;
using System.Collections.Generic;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.Logging;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.DataTransport
{

    public class GrpcWrapperException : Exception
    {
        public readonly string Status;

        public GrpcWrapperException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public GrpcWrapperException(StatusCode statusCode, string message, Exception innerException) : base(message, innerException)
        {
            Status = EnumNameCache<StatusCode>.GetNameToUpperSnakeCase(statusCode);
        }
    }

    public class GrpcWrapperChannelNotAvailableException : Exception
    {
        public GrpcWrapperChannelNotAvailableException(string message) : base(message)
        {
        }

        public GrpcWrapperChannelNotAvailableException() : this("gRPC Channel is not available")
        {
        }

        public GrpcWrapperChannelNotAvailableException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class GrpcWrapperStreamNotAvailableException : Exception
    {
        public GrpcWrapperStreamNotAvailableException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public interface IGrpcWrapper<TRequest, TResponse>
    {
        bool IsConnected { get; }
        bool CreateChannel(string host, int port, bool ssl, Metadata headers, int connectTimeoutMs, CancellationToken cancellationToken);
        bool CreateStreams(Metadata headers, int connectTimeoutMs, CancellationToken cancellationToken, out IClientStreamWriter<TRequest> requestStream, out IAsyncStreamReader<TResponse> responseStream);
        bool TrySendData(IClientStreamWriter<TRequest> stream, TRequest item, int sendTimeoutMs, CancellationToken cancellationToken);
        void TryCloseRequestStream(IClientStreamWriter<TRequest> requestStream);
        void Shutdown();
    }

    public abstract class GrpcWrapper<TRequest, TResponse> : IGrpcWrapper<TRequest, TResponse>
    {
        private readonly List<ChannelState> _notConnectedStates = new List<ChannelState> { ChannelState.TransientFailure, ChannelState.Shutdown };

        protected GrpcWrapper()
        {
        }

        private Channel _channel { get; set; }

        protected abstract AsyncDuplexStreamingCall<TRequest, TResponse> CreateStreamsImpl(Channel channel, Metadata headers, int connectTimeoutMs, CancellationToken cancellationToken);

        public bool IsConnected => _channel != null && !_notConnectedStates.Contains(_channel.State);

        public bool CreateChannel(string host, int port, bool ssl, Metadata headers, int connectTimeoutMs, CancellationToken cancellationToken)
        {
            try
            {
                // Ensure old channel disposed before new
                Shutdown();

                var credentials = ssl ? new SslCredentials() : ChannelCredentials.Insecure;
                var channel = new Channel(host, port, credentials);

                if (TestChannel(channel, headers, connectTimeoutMs, cancellationToken))
                {
                    _channel = channel;
                    return true;
                }

                // Ensure channel connection attempt shutdown on timeout
                channel.ShutdownAsync().Wait();
                return false;
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

        private bool TestChannel(Channel channel, Metadata headers, int connectTimeoutMs, CancellationToken cancellationToken)
        {
            try
            {
                if (channel.ConnectAsync().Wait(connectTimeoutMs, cancellationToken) && !_notConnectedStates.Contains(channel.State))
                {
                    using (CreateStreamsImpl(channel, headers, connectTimeoutMs, cancellationToken))
                    {
                        return true;
                    }
                }
            }
            catch (Exception) { }

            return false;
        }

        public bool CreateStreams(Metadata headers, int connectTimeoutMs, CancellationToken cancellationToken, out IClientStreamWriter<TRequest> requestStream, out IAsyncStreamReader<TResponse> responseStream)
        {
            requestStream = null;
            responseStream = null;

            try
            {
                var streams = CreateStreamsImpl(_channel, headers, connectTimeoutMs, cancellationToken);

                requestStream = streams.RequestStream;
                responseStream = streams.ResponseStream;

                return true;
            }
            catch (GrpcWrapperChannelNotAvailableException)
            {
                return false;
            }
            catch (Exception ex)
            {
                const string errorMessage = "Unable to create new gRPC Streams";

                var grpcEx = ex as RpcException ?? ex.InnerException as RpcException;

                if (grpcEx != null)
                {
                    throw new GrpcWrapperException(grpcEx.StatusCode, errorMessage, grpcEx);
                }

                throw new GrpcWrapperException(errorMessage, ex);
            }
        }


        public bool TrySendData(IClientStreamWriter<TRequest> requestStream, TRequest item, int sendTimeoutMs, CancellationToken cancellationToken)
        {
            try
            {
                //If there is no channel, return
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                return requestStream.WriteAsync(item).Wait(sendTimeoutMs, cancellationToken);
            }
            catch (Exception ex)
            {
                const string errorMessage = "Unable to create gRPC Request/Response Streams";

                var invalidOperEx = ex as InvalidOperationException;
                if (invalidOperEx != null && invalidOperEx.Message == "Request stream has already been completed.")
                {
                    throw new GrpcWrapperStreamNotAvailableException(errorMessage, invalidOperEx);
                }

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
                if (Log.IsFinestEnabled)
                {
                    Log.Finest($"{this.GetType().Name}: Error encountered shutting down gRPC channel: {ex}");
                }
            }

            _channel = null;
        }

        public void TryCloseRequestStream(IClientStreamWriter<TRequest> requestStream)
        {
            try
            {
                requestStream.CompleteAsync().Wait();
            }
            catch (Exception ex)
            {
                if (Log.IsFinestEnabled)
                {
                    Log.Finest($"{this.GetType().Name}: Error encountered closing gRPC request channel: {ex}");

                }
            }
        }
    }
}
