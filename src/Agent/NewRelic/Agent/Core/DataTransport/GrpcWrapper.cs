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
        bool CreateChannel(string host, int port, bool ssl, Metadata headers, CancellationToken cancellationToken);
        IClientStreamWriter<TRequest> CreateStreams(Metadata headers, CancellationToken cancellationToken, Action<TResponse> responseDelegate);
        bool TrySendData(IClientStreamWriter<TRequest> stream, TRequest item, int timeoutWindowMs, CancellationToken cancellationToken);
        void TryCloseRequestStream(IClientStreamWriter<TRequest> requestStream);
        void Shutdown();
        void ManageResponseStream(CancellationToken cancellationToken, IAsyncStreamReader<TResponse> responseStream, Action<TResponse> responseDelegate);
    }

    public abstract class GrpcWrapper<TRequest, TResponse> : IGrpcWrapper<TRequest, TResponse>
    {
        protected GrpcWrapper()
        {
        }

        protected ChannelBase _channel { get; private set; }

        protected abstract AsyncDuplexStreamingCall<TRequest, TResponse> CreateStreamsImpl(Metadata headers, CancellationToken cancellationToken);

        public bool IsConnected => _channel != null;

        public bool CreateChannel(string host, int port, bool ssl, Metadata headers, CancellationToken cancellationToken)
        {
            try
            {
                var credentials = ssl ? new SslCredentials() : ChannelCredentials.Insecure;
                _channel = new Channel(host, port, credentials);

                return TestChannel(headers, cancellationToken);
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

        private bool TestChannel(Metadata headers, CancellationToken cancellationToken)
        {
            using (CreateStreamsImpl(headers, cancellationToken))
            {
                return true;
            }
        }

        public IClientStreamWriter<TRequest> CreateStreams(Metadata headers, CancellationToken cancellationToken, Action<TResponse> responseDelegate)
        {
            try
            {
                var streams = CreateStreamsImpl(headers, cancellationToken);

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


        public bool TrySendData(IClientStreamWriter<TRequest> requestStream, TRequest item, int timeoutWindowMs, CancellationToken cancellationToken)
        {
            try
            {
                //If there is no channel, return
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                return requestStream.WriteAsync(item).Wait(timeoutWindowMs, cancellationToken);
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
                if (Log.IsDebugEnabled)
                {
                    Log.Debug($"Exception encountered while handling gRPC server responses: {ex}");
                }
            }
        }
    }
}
