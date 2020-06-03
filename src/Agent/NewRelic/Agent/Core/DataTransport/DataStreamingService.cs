using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.Logging;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Configuration;
using System.Text;
using System.Linq;

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
        private const string UnimplementedStatus = "UNIMPLEMENTED";
        private const string OkStatus = "OK";
        private readonly IGrpcWrapper<TRequest, TResponse> _grpcWrapper;
        private readonly IDelayer _delayer;
        protected readonly IAgentHealthReporter _agentHealthReporter;
        private readonly IAgentTimerService _agentTimerService;

        private readonly IConfigurationService _configSvc;
        protected IConfiguration _configuration => _configSvc?.Configuration;

        private readonly string _modelType = typeof(TRequest).Name;
        private readonly string _timerEventNameForSend = "gRPCSend" + typeof(TRequest).Name;
        private readonly string _timerEventNameForChannel = "gRPCCreateChannel" + typeof(TRequest).Name;
        private readonly string _timerEventNameForStream = "gRPCCreateStreams" + typeof(TRequest).Name;

        private const int _delayBetweenRpcCallsMs = 15000;

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
            var headers = new Metadata();

            headers.Add(new Metadata.Entry("agent_run_token", _configuration.AgentRunId.ToString()));
            headers.Add(new Metadata.Entry("license_key", _configuration.AgentLicenseKey));

            if (_configuration.RequestHeadersMap != null)
            {
                foreach (var requestHeader in _configuration.RequestHeadersMap)
                {
                    headers.Add(new Metadata.Entry(requestHeader.Key.ToLower(), requestHeader.Value));
                }
            }

            if (EndpointTestDelayMs.HasValue)
            {
                headers.Add(new Metadata.Entry("delay", EndpointTestDelayMs.ToString()));
            }

            if (EndpointTestFlaky.HasValue)
            {
                headers.Add(new Metadata.Entry("flaky", EndpointTestFlaky.ToString()));
            }

            if (Log.IsFinestEnabled)
            {
                var parametersString = string.Join(",", headers.Select(x => $"{x.Key}={x.Value}"));
                LogMessage(LogLevel.Finest, $"Creating gRPC Metadata ({parametersString})");
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
        protected abstract void RecordSendTimeout();

        private bool? _isServiceEnabled;
        public bool IsServiceEnabled => _isServiceEnabled ?? (_isServiceEnabled = ReadAndValidateConfiguration()).Value;
        public bool IsServiceAvailable => IsServiceEnabled && _grpcWrapper.IsConnected && IsConfigurationValid;

        private BlockingCollection<TRequest> _collection;

        protected DataStreamingService(IGrpcWrapper<TRequest, TResponse> grpcWrapper, IDelayer delayer, IConfigurationService configSvc, IAgentHealthReporter agentHealthReporter, IAgentTimerService agentTimerService)
        {
            _grpcWrapper = grpcWrapper;
            _delayer = delayer;
            _configSvc = configSvc;
            _agentTimerService = agentTimerService;

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
            if (string.IsNullOrWhiteSpace(configPortStr))
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

            if (!isValidTimeoutConnect)
            {
                LogMessage(LogLevel.Info, $"Invalid Configuration.  Timeout Connect Ms '{TimeoutConnectMs}' is not valid.  Infinite Tracing will NOT be started.");
            }

            if (!isValidTimeoutSend)
            {
                LogMessage(LogLevel.Info, $"Invalid Configuration.  Timeout Send Ms '{TimeoutSendDataMs}' is not valid.  Infinite Tracing will NOT be started.");
            }

            return false;
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

                    var createdChannel = false;
                    using (_agentTimerService.StartNew(_timerEventNameForChannel))
                    {
                        createdChannel = _grpcWrapper.CreateChannel(EndpointHost, EndpointPort, MetadataHeaders, cancellationToken);
                    }

                    if (createdChannel)
                    {
                        LogMessage(LogLevel.Finest, $"gRPC channel to endpoint {EndpointHost}:{EndpointPort} connected.");
                        return true;
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    LogMessage(LogLevel.Debug, $"Timeout creating gRPC channel at endpoint {EndpointHost}:{EndpointPort}. (attempt {attemptId}, timeout {TimeoutConnectMs}ms)");
                }
                catch (Exception ex)
                {
                    LogMessage(LogLevel.Debug, $"Error creating gRPC channel to endpoint {EndpointHost}:{EndpointPort}. (attempt {attemptId})", ex);
                    RecordResponseError();

                    var grpcWrapperEx = ex as GrpcWrapperException;
                    if (grpcWrapperEx != null && !string.IsNullOrWhiteSpace(grpcWrapperEx.Status))
                    {
                        RecordGrpcError(grpcWrapperEx.Status);

                        if (grpcWrapperEx.Status == UnimplementedStatus)
                        {
                            LogMessage(LogLevel.Error, $"The gRPC endpoint defined at {EndpointHost}:{EndpointPort} is not available and no reconnection attempts will be made.");
                            Shutdown();
                            return false;
                        }

                        if (grpcWrapperEx.Status == OkStatus)
                        {
                            //Getting the OK status back indicates that the channel was created successfully, but the test stream
                            //needed to be rebalanced to another handler. Since the test stream is only used to ensure that the
                            //channel is valid we can just return true here.
                            LogMessage(LogLevel.Finest, $"gRPC channel to endpoint {EndpointHost}:{EndpointPort} connected.");
                            return true;
                        }
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
            catch (Exception ex)
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

        private IClientStreamWriter<TRequest> GetRequestStreamWithRetry(int consumerId, CancellationToken cancellationToken)
        {
            var attemptId = 0;

            LogMessage(LogLevel.Finest, consumerId, $"Creating gRPC request stream (attempt {attemptId}).");

            while (!cancellationToken.IsCancellationRequested && IsServiceEnabled)
            {
                var shouldRetryImmediately = false;

                try
                {
                    using (_agentTimerService.StartNew(_timerEventNameForStream))
                    {
                        var requestStream = _grpcWrapper.CreateStreams(MetadataHeaders, cancellationToken, handleResponseFunc);

                        if (requestStream != null)
                        {
                            LogMessage(LogLevel.Finest, consumerId, $"gRPC request stream connected (attempt {attemptId}).");
                            return requestStream;
                        }
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    LogMessage(LogLevel.Debug, consumerId, $"Error creating gRPC request stream. (attempt {attemptId})", ex);
                    RecordResponseError();

                    var grpcWrapperEx = ex as GrpcWrapperException;
                    if (grpcWrapperEx != null && !string.IsNullOrWhiteSpace(grpcWrapperEx.Status))
                    {
                        RecordGrpcError(grpcWrapperEx.Status);

                        if (grpcWrapperEx.Status == UnimplementedStatus)
                        {
                            LogMessage(LogLevel.Error, consumerId, $"The gRPC request stream could not be created because the gRPC endpoint defined at {EndpointHost}:{EndpointPort} is no longer available and no reconnection attempts will be made.");
                            Shutdown();
                            return null;
                        }

                        if (grpcWrapperEx.Status == OkStatus)
                        {
                            LogMessage(LogLevel.Debug, consumerId, $"The gRPC request stream could not be created because the gRPC endpoint defined at {EndpointHost}:{EndpointPort} requested that the stream needed to be rebalanced so we should attempt to connect again immediately. (attempt {attemptId})", ex);
                            shouldRetryImmediately = true;
                        }
                    }
                }

                var delayIdx = Math.Min(attemptId, _backoffDelaySequenceConnectMs.Length - 1);
                var delayPeriodMs = shouldRetryImmediately ? 0 : _backoffDelaySequenceConnectMs[delayIdx];

                LogMessage(LogLevel.Finest, consumerId, $"Backoff for {delayPeriodMs}ms before attempting to reconnect.");

                _delayer.Delay(delayPeriodMs, cancellationToken);

                //If we need to retry immediately, we should restart the attempt counter because it was "successfully" connected but needed to be rebalanced.
                attemptId = shouldRetryImmediately ? 0 : attemptId + 1;
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
            if (collection == null)
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

            while (!cancellationToken.IsCancellationRequested)
            {
                var shouldRetryImmediately = false;

                try
                {
                    shouldRetryImmediately = StreamRequests(collection, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    LogMessage(LogLevel.Finest, "Got exception while attempting to StreamRequests.", ex);
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    var delayInMs = shouldRetryImmediately ? 0 : _delayBetweenRpcCallsMs;
                    _delayer.Delay(delayInMs, cancellationToken);
                }
            }
        }

        private bool StreamRequests(BlockingCollection<TRequest> collection, CancellationToken serviceCancellationToken)
        {
            var consumerId = _consumerId.Increment();
            using (var streamCancellationTokenSource = new CancellationTokenSource())
            using (var serviceAndStreamCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(serviceCancellationToken, streamCancellationTokenSource.Token))
            {
                var requestStream = GetRequestStreamWithRetry(consumerId, serviceAndStreamCancellationTokenSource.Token);

                if (requestStream == null)
                {
                    LogMessage(LogLevel.Debug, consumerId, "Unable to obtain Stream, exiting consumer");
                    streamCancellationTokenSource.Cancel();
                    return false;
                }

                while (!serviceCancellationToken.IsCancellationRequested && IsServiceAvailable)
                {
                    TRequest item;
                    item = collection.Take(serviceCancellationToken);

                    if (item == null)
                    {
                        LogMessage(LogLevel.Debug, consumerId, $"Expected a {_modelType} from the collection, but it was null");
                        continue;
                    }

                    var trySendStatus = TrySend(consumerId, requestStream, item, serviceCancellationToken);
                    if (trySendStatus != TrySendStatus.Success)
                    {
                        collection.Add(item);
                        _grpcWrapper.TryCloseRequestStream(requestStream);
                        streamCancellationTokenSource.Cancel();
                        return trySendStatus == TrySendStatus.ErrorWithImmediateRetry;
                    }
                }

                streamCancellationTokenSource.Cancel();
            }

            return false;
        }

        private TrySendStatus TrySend(int consumerId, IClientStreamWriter<TRequest> requestStream, TRequest item, CancellationToken cancellationToken)
        {
            //If there is no channel, return
            if (cancellationToken.IsCancellationRequested)
            {
                return TrySendStatus.CancellationRequested;
            }

            LogMessage(LogLevel.Finest, consumerId, item, $"Attempting to send");

            try
            {
                var sentData = false;
                using (_agentTimerService.StartNew(_timerEventNameForSend))
                {
                    sentData = _grpcWrapper.TrySendData(requestStream, item, TimeoutSendDataMs, cancellationToken);
                }

                if (sentData)
                {
                    LogMessage(LogLevel.Finest, consumerId, item, $"Attempting to send - Success");
                    RecordSuccessfulSend();
                    return TrySendStatus.Success;
                }

                RecordSendTimeout();
                LogMessage(LogLevel.Finest, consumerId, item, $"Attempting to send - Timed Out");
            }
            catch (GrpcWrapperStreamNotAvailableException streamNotAvailEx)
            {
                LogMessage(LogLevel.Finest, consumerId, item, $"Attempting to send - Request stream closed.", streamNotAvailEx);
                RecordResponseError();
            }
            catch (GrpcWrapperException grpcEx) when (!string.IsNullOrWhiteSpace(grpcEx.Status))
            {
                LogMessage(LogLevel.Finest, consumerId, item, $"Attempting to send - gRPC Exception: {grpcEx.Status}", grpcEx);

                RecordResponseError();
                RecordGrpcError(grpcEx.Status);

                if (grpcEx.Status == UnimplementedStatus)
                {
                    Shutdown();
                    return TrySendStatus.Error;
                }

                if (grpcEx.Status == OkStatus)
                {
                    LogMessage(LogLevel.Finest, consumerId, item, $"Attempting to send - New stream requested");
                    return TrySendStatus.ErrorWithImmediateRetry;
                }
            }
            catch (Exception ex)
            {
                LogMessage(LogLevel.Finest, consumerId, item, $"Attempting to send", ex);
                RecordResponseError();
            }

            return TrySendStatus.Error;
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

        private enum TrySendStatus
        {
            Success,
            CancellationRequested,
            Error,
            ErrorWithImmediateRetry
        }
    }
}
