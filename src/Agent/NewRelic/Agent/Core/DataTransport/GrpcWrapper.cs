/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
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
        IClientStreamWriter<TRequest> CreateStreams(Metadata headers, int connectTimeoutMs, CancellationToken cancellationToken, Action<TResponse> responseDelegate);
        bool TrySendData(IClientStreamWriter<TRequest> stream, TRequest item, int sendTimeoutMs, CancellationToken cancellationToken);
        void TryCloseRequestStream(IClientStreamWriter<TRequest> requestStream);
        void Shutdown();
        void ManageResponseStream(CancellationToken cancellationToken, IAsyncStreamReader<TResponse> responseStream, Action<TResponse> responseDelegate);
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
                _channel = null;

                var credentials = ssl ? new SslCredentials() : ChannelCredentials.Insecure;
                var channel = new Channel(host, port, credentials);

                if (TestChannel(channel, headers, connectTimeoutMs, cancellationToken))
                {
                    _channel = channel;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _channel = null;

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
                if (!channel.ConnectAsync().Wait(connectTimeoutMs, cancellationToken) || _notConnectedStates.Contains(channel.State))
                {
                    return false;
                }

                using (CreateStreamsImpl(channel, headers, connectTimeoutMs, cancellationToken))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public IClientStreamWriter<TRequest> CreateStreams(Metadata headers, int connectTimeoutMs, CancellationToken cancellationToken, Action<TResponse> responseDelegate)
        {
            try
            {
                var streams = CreateStreamsImpl(_channel, headers, connectTimeoutMs, cancellationToken);

                if (responseDelegate != null)
                {
                    Task.Run(() => ManageResponseStream(cancellationToken, streams.ResponseStream, responseDelegate));
                }

                return streams.RequestStream;
            }
            catch (GrpcWrapperChannelNotAvailableException)
            {
                return null;
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
                _channel.ShutdownAsync();
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
                requestStream.CompleteAsync();
            }
            catch (Exception ex)
            {
                if (Log.IsFinestEnabled)
                {
                    Log.Finest($"{this.GetType().Name}: Error encountered closing gRPC request channel: {ex}");

                }
            }
        }

        public void ManageResponseStream(CancellationToken cancellationToken, IAsyncStreamReader<TResponse> responseStream, Action<TResponse> responseDelegate)
        {
            try
            {
                while (responseStream.MoveNext(cancellationToken).Result)
                {
                    var response = responseStream.Current;

                    if (response != null)
                    {
                        responseDelegate(response);
                    }
                }
            }
            catch (Exception ex)
            {
                var logLevel = LogLevel.Finest;

                var aggEx = ex as AggregateException;
                if (aggEx != null && aggEx.InnerException != null)
                {
                    var rpcEx = aggEx.InnerException as RpcException;

                    logLevel = (rpcEx != null && rpcEx.StatusCode == StatusCode.Cancelled)
                        ? LogLevel.Finest
                        : LogLevel.Debug;
                }

                if (Log.IsEnabledFor(logLevel))
                {
                    Log.LogMessage(logLevel, $"Exception encountered while handling gRPC server responses: {ex}");
                }
            }
        }
    }
}
