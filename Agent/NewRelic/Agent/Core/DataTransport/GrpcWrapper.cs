using System;
using Grpc.Core;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
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
		bool CreateChannel(string host, int port, int timeoutMs, CancellationToken cancellationToken, int attemptId);
		IClientStreamWriter<TRequest> CreateStreams(Metadata headers, CancellationToken cancellationToken, Action<TResponse> responseDelegate);
		bool TrySendData(IClientStreamWriter<TRequest> stream, TRequest item, int timeoutWindowMs, CancellationToken cancellationToken, int attemptId);
		void TryCloseRequestStream(IClientStreamWriter<TRequest> requestStream);
		void Shutdown();
		ChannelState OnChannelStateChanged(CancellationToken cancellationToken);
		ChannelState? CurrentState { get; }
		void ManageResponseStream(CancellationToken cancellationToken, IAsyncStreamReader<TResponse> responseStream, Action<TResponse> responseDelegate);
	}

	public abstract class GrpcWrapper<TRequest, TResponse> : IGrpcWrapper<TRequest, TResponse>
	{
		protected GrpcWrapper()
		{
		}

		private static readonly ChannelState[] _connectedStates = new[] { ChannelState.Idle, ChannelState.Ready, ChannelState.TransientFailure };

		protected Channel _channel { get; private set; }

		public ChannelState? CurrentState => _channel?.State;

		private readonly List<IClientStreamWriter<TRequest>> _requestStreams = new List<IClientStreamWriter<TRequest>>();

		protected abstract AsyncDuplexStreamingCall<TRequest, TResponse> CreateStreamsImpl(Metadata headers, CancellationToken cancellationToken);

		public bool IsConnected => _channel != null && _connectedStates.Contains(_channel.State);

		public bool CreateChannel(string host, int port, int timeoutMs, CancellationToken cancellationToken, int attemptId)
		{
			try
			{
				_channel = new Channel(host, port, new SslCredentials());

				return _channel.ConnectAsync().Wait(timeoutMs, cancellationToken);
			}
			catch(Exception ex)
			{
				const string errorMessage = "Unable to create new gRPC Channel";
				
				var grpcEx = ex as RpcException ?? ex.InnerException as RpcException;
				
				if(grpcEx != null)
				{
					throw new GrpcWrapperException(grpcEx.StatusCode, errorMessage, grpcEx);
				}

				throw new GrpcWrapperException(errorMessage, ex);
			}
		}

		public ChannelState OnChannelStateChanged(CancellationToken cancellationToken)
		{
			var channel = _channel;
			
			if(channel == null || channel.State == ChannelState.Shutdown)
			{
				return ChannelState.Shutdown;
			}

			channel.WaitForStateChangedAsync(channel.State).Wait(cancellationToken);

			return channel.State;
		}

		public IClientStreamWriter<TRequest> CreateStreams(Metadata headers, CancellationToken cancellationToken, Action<TResponse> responseDelegate)
		{
			try
			{
				var streams = CreateStreamsImpl(headers, cancellationToken);
	
				_requestStreams.Add(streams.RequestStream);

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


		public bool TrySendData(IClientStreamWriter<TRequest> requestStream, TRequest item, int timeoutWindowMs, CancellationToken cancellationToken, int attemptId)
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
			catch(Exception ex)
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

			foreach (var requestStream in _requestStreams)
			{
				TryCloseRequestStream(requestStream);
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

			_requestStreams.Clear();
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
