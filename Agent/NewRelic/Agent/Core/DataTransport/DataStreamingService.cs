using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.Logging;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.DataTransport
{
	public interface IDataStreamingService<TRequest, TResponse> : IDisposable
		where TRequest : IStreamingModel
	{
		bool IsServiceAvailable { get; }
		bool IsServiceEnabled { get; }
		string EndpointHost { get; }
		int EndpointPort { get; }
		float? EndpointTestFlaky { get; }
		int? EndpointTestDelayMs { get; }
		int TimeoutConnectMs { get; }
		int TimeoutSendDataMs { get; }
		void Shutdown();
		void StartConsumingCollection(BlockingCollection<TRequest> collection);
		bool ReadAndValidateConfiguration();
	}

	public abstract class DataStreamingService<TRequest, TResponse> : IDataStreamingService<TRequest, TResponse>
		where TRequest : class, IStreamingModel
	{
		private readonly IGrpcWrapper<TRequest, TResponse> _grpcWrapper;
		private readonly IDelayer _delayer;
		protected readonly IAgentHealthReporter _agentHealthReporter;

		private readonly IConfigurationService _configSvc;
		protected IConfiguration _configuration => _configSvc?.Configuration;

		private readonly string _modelType = typeof(TRequest).Name;

		private readonly int[] _backoffDelaySequenceData = new[]
		{
			15000,
			15000,
			15000
		};

		private readonly int[] _backoffDelaySequenceConnectMs = new[]
		{
			15000,
			15000,
			30000,
			60000,
			120000,
			300000
		};

		public int TimeoutConnectMs { get; private set; }
		public int TimeoutSendDataMs { get; private set; }

		private bool? _isConfigurationValid;
		protected bool IsConfigurationValid => (_isConfigurationValid ?? (_isConfigurationValid = ReadAndValidateConfiguration())).Value;

		/// <summary>
		/// ConsumerID is helpful for correlating log messages when there are multiple
		/// consumer threads running
		/// </summary>
		private readonly InterlockedCounter _consumerId = new InterlockedCounter();

		protected Metadata MetadataHeaders { get; private set; }
		private Metadata CreateMetadataHeaders()
		{
			LogMessage(LogLevel.Finest, $"Creating gRPC Metadata (license_key={_configuration.AgentLicenseKey}, agent_run_token={_configuration.AgentRunId}, flaky={EndpointTestFlaky?.ToString() ?? "NULL"}, delay={EndpointTestDelayMs?.ToString()??"NULL"})");

			var headers = new Metadata();

			headers.Add(new Metadata.Entry("agent_run_token", _configuration.AgentRunId.ToString()));
			headers.Add(new Metadata.Entry("license_key", _configuration.AgentLicenseKey));

			if(EndpointTestDelayMs.HasValue)
			{
				headers.Add(new Metadata.Entry("delay", EndpointTestDelayMs.ToString()));
			}

			if (EndpointTestFlaky.HasValue)
			{
				headers.Add(new Metadata.Entry("flaky", EndpointTestFlaky.ToString()));
			}

			return headers;
		}

		private CancellationTokenSource _cancellationTokenSource;

		protected CancellationToken CancellationToken => _cancellationTokenSource.Token;

		public string EndpointHost { get; private set; }
		public int EndpointPort { get; private set; }
		public float? EndpointTestFlaky { get; private set; }
		public int? EndpointTestDelayMs { get; private set; }

		protected abstract string EndpointHostConfigValue { get; }
		protected abstract string EndpointPortConfigValue { get; }
		protected abstract float? EndpointTestFlakyConfigValue { get; }
		protected abstract int? EndpointTestDelayMsConfigValue { get; }

		protected abstract void HandleServerResponse(TResponse responseModel, int consumerId);

		protected abstract void RecordSuccessfulSend();
		protected abstract void RecordGrpcError(string status);
		protected abstract void RecordResponseError();
		protected abstract void RecordSendTimeout(int attemptId);

		private bool? _isServiceEnabled;
		public bool IsServiceEnabled => _isServiceEnabled ?? (_isServiceEnabled = ReadAndValidateConfiguration()).Value;
		public bool IsServiceAvailable => IsServiceEnabled && _grpcWrapper.IsConnected && IsConfigurationValid;

		private BlockingCollection<TRequest> _collection;

		protected DataStreamingService(IGrpcWrapper<TRequest, TResponse> grpcWrapper, IDelayer delayer, IConfigurationService configSvc, IAgentHealthReporter agentHealthReporter)
		{
			_grpcWrapper = grpcWrapper;
			_delayer = delayer;
			_configSvc = configSvc;

			_cancellationTokenSource = new CancellationTokenSource();
			_agentHealthReporter = agentHealthReporter;

			//This will ensure that anything that depends on the token will not run until
			//we are ready (ie. we have called Start which will generate a new token).
			_cancellationTokenSource.Cancel();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns>boolean indicating if the service is enabled</returns>
		public bool ReadAndValidateConfiguration()
		{
			TimeoutConnectMs = _configuration.InfiniteTracingTraceTimeoutMsConnect;
			TimeoutSendDataMs = _configuration.InfiniteTracingTraceTimeoutMsSendData;

			var configHost = EndpointHostConfigValue;
			var configPortStr = EndpointPortConfigValue;
			if(string.IsNullOrWhiteSpace(configPortStr))
			{
				configPortStr = "443";
			}

			//Infinite Tracing Disabled
			if (string.IsNullOrWhiteSpace(configHost))
			{
				EndpointHost = null;
				EndpointPort = -1;
				EndpointTestFlaky = null;
				EndpointTestDelayMs = null;

				return false;
			}

			var isValidHost = Uri.CheckHostName(configHost) != UriHostNameType.Unknown;
			var isValidPort = int.TryParse(configPortStr, out var configPort) && configPort > 0 && configPort <= 65535;
			var isValidFlaky = EndpointTestFlakyConfigValue == null || (EndpointTestFlakyConfigValue >= 0 && EndpointTestFlakyConfigValue <= 100);
			var isValidDelay = EndpointTestDelayMsConfigValue == null || (EndpointTestDelayMsConfigValue >= 0);
			var isValidTimeoutConnect = TimeoutConnectMs > 0;
			var isValidTimeoutSend = TimeoutSendDataMs > 0;

			if (isValidHost && isValidPort && isValidFlaky && isValidDelay && isValidTimeoutConnect && isValidTimeoutSend)
			{
				EndpointHost = configHost;
				EndpointPort = configPort;
				EndpointTestFlaky = EndpointTestFlakyConfigValue;
				EndpointTestDelayMs = EndpointTestDelayMsConfigValue;

				return true;
			}

			EndpointHost = null;
			EndpointPort = -1;
			EndpointTestFlaky = null;
			EndpointTestDelayMs = null;

			if (!isValidHost)
			{
				LogMessage(LogLevel.Info, $"Invalid Configuration.  Endpoint Host '{configHost}' is not valid.  Infinite Tracing will NOT be started.");
			}

			if (!isValidPort)
			{
				LogMessage(LogLevel.Info, $"Invalid Configuration.  Endpoint Port '{configPortStr}' is not valid.  Infinite Tracing will NOT be started.");
			}

			if (!isValidFlaky)
			{
				LogMessage(LogLevel.Info, $"Invalid Configuration For Test.  Flaky % '{EndpointTestFlakyConfigValue}' is not valid.  Infinite Tracing will NOT be started.");
			}

			if (!isValidDelay)
			{
				LogMessage(LogLevel.Info, $"Invalid Configuration For Test.  Delay Ms '{EndpointTestDelayMsConfigValue}' is not valid.  Infinite Tracing will NOT be started.");
			}

			if(!isValidTimeoutConnect)
			{
				LogMessage(LogLevel.Info, $"Invalid Configuration.  Timeout Connect Ms '{TimeoutConnectMs}' is not valid.  Infinite Tracing will NOT be started.");
			}

			if (!isValidTimeoutSend)
			{
				LogMessage(LogLevel.Info, $"Invalid Configuration.  Timeout Send Ms '{TimeoutSendDataMs}' is not valid.  Infinite Tracing will NOT be started.");
			}

			return false;
		}

		private void Restart()
		{
			Restart(_collection);
		}

		private void Restart(BlockingCollection<TRequest> collection)
		{
			_grpcWrapper.Shutdown();

			_collection = collection;

			if (StartService())
			{
				StartConsumers();
			}
		}

		private void StartConsumers()
		{
			//Check to make sure that we actually connected to the grpcService and that
			//streaming is enabled
			if (!IsServiceAvailable)
			{
				return;
			}

			//Start up the workers
			for (var i = 0; i < _configuration.InfiniteTracingTraceCountConsumers; i++)
			{
				Task.Run(() => ExecuteConsumer(_collection));
			}
		}

		private bool StartService()
		{
			_isConfigurationValid = ReadAndValidateConfiguration();

			if (!IsServiceEnabled || !IsConfigurationValid)
			{
				return false;
			}

			LogConfigurationSettings();

			MetadataHeaders = CreateMetadataHeaders();

			_cancellationTokenSource = new CancellationTokenSource();

			return CreateChannel(_cancellationTokenSource.Token);
		}

		private void LogConfigurationSettings()
		{
			LogMessage(LogLevel.Info, $"Configuration Setting - Host - {EndpointHost}");
			LogMessage(LogLevel.Info, $"Configuration Setting - Port - {EndpointPort}");
			LogMessage(LogLevel.Info, $"Configuration Setting - Consumers - {_configuration.InfiniteTracingTraceCountConsumers}");
			LogMessage(LogLevel.Finest, $"Configuration Setting - Test Flaky - {EndpointTestFlaky?.ToString() ?? "NULL"}");
			LogMessage(LogLevel.Finest, $"Configuration Setting - Test Delay (ms) - {EndpointTestDelayMs?.ToString() ?? "NULL"}");
		}

		private bool CreateChannel(CancellationToken cancellationToken)
		{
			_grpcWrapper.Shutdown();

			var attemptId = 0;

			LogMessage(LogLevel.Finest, $"Creating gRPC channel to endpoint {EndpointHost}:{EndpointPort}. (attempt {attemptId})");

			while (!cancellationToken.IsCancellationRequested && IsServiceEnabled)
			{
				try
				{
					if(_grpcWrapper.CreateChannel(EndpointHost, EndpointPort, TimeoutConnectMs, cancellationToken, attemptId))
					{
						LogMessage(LogLevel.Finest, $"gRPC channel to endpoint {EndpointHost}:{EndpointPort} connected.");
						Task.Run(() => HandleChannelStateChanges(cancellationToken));
						return true;
					}

					if(cancellationToken.IsCancellationRequested)
					{
						break;
					}

					LogMessage(LogLevel.Debug, $"Timeout creating gRPC channel at endpoint {EndpointHost}:{EndpointPort}. (attempt {attemptId}, timeout {TimeoutConnectMs}ms)");
				}
				catch (Exception ex)
				{
					LogMessage(LogLevel.Debug, $"Error creating gRPC channel to endpoint {EndpointHost}:{EndpointPort}. (attempt {attemptId})",ex);
					RecordResponseError();

					var grpcWrapperEx = ex as GrpcWrapperException;
					if(grpcWrapperEx != null && !string.IsNullOrWhiteSpace(grpcWrapperEx.Status))
					{
						RecordGrpcError(grpcWrapperEx.Status);
					}
				}

				var delayIdx = Math.Min(attemptId, _backoffDelaySequenceConnectMs.Length - 1);
				var delayPeriodMs = _backoffDelaySequenceConnectMs[delayIdx];

				LogMessage(LogLevel.Finest, $"Backoff for {delayPeriodMs}ms before attempting to reconnect.");

				_delayer.Delay(delayPeriodMs, cancellationToken);

				attemptId++;
			}

			return false;
		}

		public void Dispose()
		{
			try
			{
				Shutdown();
			}
			catch(Exception ex)
			{
				LogMessage(LogLevel.Finest, $"Exception during dispose", ex);
			}
		}

		public void Shutdown()
		{
			LogMessage(LogLevel.Debug, "Shutdown Request Received");

			_cancellationTokenSource.Cancel();
			MetadataHeaders = null;
			_grpcWrapper.Shutdown();
		}

		/// <summary>
		/// This method is designed to be called from an external Task.
		/// It will wait for a state change indefinitely.
		/// </summary>
		/// <param name="token"></param>
		private void HandleChannelStateChanges(CancellationToken token)
		{
			while (!token.IsCancellationRequested)
			{
				var oldState = _grpcWrapper.CurrentState;
				var newState = _grpcWrapper.OnChannelStateChanged(token);

				//If cancellation has been requested, we don't need to shut down because
				//the cancellation is likely b/c of a shutdown request.
				if(token.IsCancellationRequested)
				{
					return;
				}

				LogMessage(LogLevel.Finest, $"gRPC Channel State Changed from '{oldState?.ToString() ?? "NULL"}' to '{newState}'.");

				if(newState == ChannelState.Shutdown)
				{
					Restart();
					return;
				}
			}
		}

		private IClientStreamWriter<TRequest> GetRequestStream(int consumerId, CancellationToken cancellationToken)
		{
			try
			{
				var requestStream = _grpcWrapper.CreateStreams(MetadataHeaders, cancellationToken, handleResponseFunc);
				return requestStream;
			}
			catch (Exception ex)
			{
				if (Log.IsDebugEnabled)
				{
					LogMessage(LogLevel.Debug, consumerId, "Unable to create gRPC Request/Response Streams", ex);
				}
			}

			return null;

			void handleResponseFunc(TResponse responseMsg)
			{
				HandleServerResponse(responseMsg, consumerId);
			}
		}

		/// <summary>
		/// Designed to be called by the aggregator.
		/// </summary>
		/// <param name="collection"></param>
		public void StartConsumingCollection(BlockingCollection<TRequest> collection)
		{
			if(collection == null)
			{
				Log.Debug("Unable to start Data Streaming Service because queue was NULL.");
				return;
			}

			Task.Run(() =>
			{
				Restart(collection);
			});
		}

		/// <summary>
		/// Wraps the consumer ensuring that the consumer task continues to run
		/// in the event of unforeseen failures.
		/// </summary>
		/// <param name="collection"></param>
		private void ExecuteConsumer(BlockingCollection<TRequest> collection)
		{
			var cancellationToken = CancellationToken;

			while(!cancellationToken.IsCancellationRequested)
			{
				try
				{
					StreamRequests(collection, cancellationToken);
				}
				catch (OperationCanceledException)
				{
					return;
				}
				catch (Exception ex)
				{
					LogMessage(LogLevel.Finest, "Got exception while attempting to StreamRequests.", ex);
				}
			}
		}

		private void StreamRequests(BlockingCollection<TRequest> collection, CancellationToken cancellationToken)
		{
			var consumerId = _consumerId.Increment();
			var requestStream = GetRequestStream(consumerId, cancellationToken);

			if (requestStream == null)
			{
				LogMessage(LogLevel.Debug, consumerId, "Unable to obtain Stream, exiting consumer");
				return;
			}

			while (!cancellationToken.IsCancellationRequested && IsServiceAvailable)
			{
				TRequest item;
				item = collection.Take(cancellationToken);

				if (item == null)
				{
					LogMessage(LogLevel.Debug, consumerId, $"Expected a {_modelType} from the collection, but it was null");
					continue;
				}

				if (!TrySend(consumerId, requestStream, item, 0, cancellationToken))
				{
					collection.Add(item);
					_grpcWrapper.TryCloseRequestStream(requestStream);
					return;
				}
			}
		}

		private bool TrySend(int consumerId, IClientStreamWriter<TRequest> requestStream, TRequest item, int attemptId, CancellationToken cancellationToken)
		{
			//If there is no channel, return
			if(cancellationToken.IsCancellationRequested)
			{
				return false;
			}
			
			LogMessage(LogLevel.Finest, consumerId, item, $"Attempting to send (attempt {attemptId})");

			var wasClosedStream = false;
			try
			{
				if(_grpcWrapper.TrySendData(requestStream, item, TimeoutSendDataMs, cancellationToken, attemptId))
				{
					LogMessage(LogLevel.Finest, consumerId, item, $"Attempting to send (attempt {attemptId}) - Success");
					RecordSuccessfulSend();
					return true;
				}

				RecordSendTimeout(attemptId);
				LogMessage(LogLevel.Finest, consumerId, item, $"Attempting to send (attempt {attemptId}) - Timed Out");
			}
			catch (GrpcWrapperStreamNotAvailableException streamNotAvailEx)
			{
				LogMessage(LogLevel.Finest, consumerId, item, $"Attempting to send (attempt {attemptId}) - Request stream closed, immediate retry with new Streams.", streamNotAvailEx);
				wasClosedStream = true;
			}
			catch (GrpcWrapperException grpcEx) when (!string.IsNullOrWhiteSpace(grpcEx.Status))
			{
				LogMessage(LogLevel.Finest, consumerId, item, $"Attempting to send (attempt {attemptId}) - gRPC Exception: {grpcEx.Status}", grpcEx);

				RecordResponseError();
				RecordGrpcError(grpcEx.Status);

				if (grpcEx.Status == "UNIMPLEMENTED")
				{
					Shutdown();
					return false;
				}
			}
			catch (Exception ex)
			{
				LogMessage(LogLevel.Finest, consumerId, item, $"Attempting to send (attempt {attemptId})", ex);
				RecordResponseError();
			}

			return HandleRetry(consumerId, requestStream, item, attemptId, cancellationToken, wasClosedStream);
		}

		private bool HandleRetry(int consumerId, IClientStreamWriter<TRequest> requestStream, TRequest item, int attemptId, CancellationToken cancellationToken, bool skipDelay)
		{
			//To be safe, recycle the stream where the error occurred and create a new
			//stream on the channel.
			_grpcWrapper.TryCloseRequestStream(requestStream);

			if ((attemptId + 1) > _backoffDelaySequenceData.Length)
			{
				return false;
			}

			//Do not delay if the retry is called because the stream was closed by the server,
			if (!skipDelay)
			{
				_delayer.Delay(_backoffDelaySequenceData[attemptId], cancellationToken);
			}

			requestStream = GetRequestStream(consumerId, _cancellationTokenSource.Token);

			if(requestStream == null)
			{
				return false;
			}

			return TrySend(consumerId, requestStream, item, attemptId + 1, cancellationToken);
		}

		protected void LogMessage(LogLevel level, string message, Exception ex = null)
		{
			if (Log.IsEnabledFor(level))
			{
				Log.LogMessage(level, $"{GetType().Name}: {message} {(ex == null ? string.Empty : $" - Exception: {ex}")}");
			}
		}

		protected void LogMessage(LogLevel level, int consumerId, string message, Exception ex = null)
		{
			if (Log.IsEnabledFor(level))
			{
				LogMessage(level, $"consumer {consumerId} - {message}", ex);
			}
		}

		protected void LogMessage(LogLevel level, int consumerId, TRequest item, string message, Exception ex = null)
		{
			if (Log.IsEnabledFor(level))
			{
				LogMessage(level, consumerId, $"{_modelType} {item.DisplayName} - {message}", ex);
			}
		}
	}
}
