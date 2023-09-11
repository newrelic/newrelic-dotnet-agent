// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Collections;
using NewRelic.Core;
using NewRelic.Core.Logging;
using NewRelic.SystemInterfaces;

namespace NewRelic.Agent.Core.DataTransport
{
    public class ResponseStreamWrapper<TResponse>
    {
        private const string NoStatusMessage = "No grpc-status found on response.";
        public readonly int ConsumerID;
        
        private bool _isInvalid = false;
        public bool IsInvalid => _streamCancellationToken.IsCancellationRequested || _isInvalid || (_task?.IsFaulted).GetValueOrDefault(false);

        public RpcException ResponseRpcException = null;

        private readonly IAsyncStreamReader<TResponse> _responseStream;
        private readonly CancellationToken _streamCancellationToken;
        private readonly IAgentHealthReporter _healthReporter;

        private Task<int> _task;

        public ResponseStreamWrapper(int consumerID, IAsyncStreamReader<TResponse> responseStream,
            CancellationToken streamCancellationToken, IAgentHealthReporter agentHealthReporter)
        {
            ConsumerID = consumerID;
            _responseStream = responseStream;
            _streamCancellationToken = streamCancellationToken;
            _healthReporter = agentHealthReporter;
        }

        private async Task<int> WaitForResponse()
        {
            var success = false;
            try
            {
                success = await _responseStream.MoveNext(_streamCancellationToken);
            }
            catch (RpcException rpcEx)
            {
                // **IMPORTANT** We see different errors reported in this handler for grpc.core vs grpc.net
                ResponseRpcException = rpcEx;
                _healthReporter.ReportInfiniteTracingSpanResponseError();
                _healthReporter.ReportInfiniteTracingSpanGrpcError(EnumNameCache<StatusCode>.GetNameToUpperSnakeCase(rpcEx.StatusCode));

                var logLevel = LogLevel.Finest;
                if (Log.IsEnabledFor(logLevel))
                {
                    if (rpcEx.Status.StatusCode == StatusCode.Cancelled && rpcEx.Status.Detail == NoStatusMessage)
                    {
                        Log.LogMessage(logLevel, rpcEx, $"ResponseStreamWrapper: consumer {ConsumerID} - gRPC RpcException encountered marking the response stream as cancelled. This occurs when a stream has been inactive for period of time.  A new stream will be created when needed.");
                    }
                    else
                    {
                        Log.LogMessage(logLevel, rpcEx, $"ResponseStreamWrapper: consumer {ConsumerID} - gRPC RpcException encountered while handling gRPC server responses.");
                    }
                }
            }
            catch (Exception ex)
            {
                _healthReporter.ReportInfiniteTracingSpanResponseError();

                var logLevel = LogLevel.Debug;
                if (Log.IsEnabledFor(logLevel))
                {
                    Log.LogMessage(logLevel, ex, $"ResponseStreamWrapper: consumer {ConsumerID} - Unknown exception encountered while handling gRPC server responses.");
                }
            }

            _isInvalid = !success || _streamCancellationToken.IsCancellationRequested;

            return ConsumerID;
        }

        public Task<int> GetAwaiter()
        {
            return _task ?? (_task = WaitForResponse());
        }

        public TResponse RetrieveResponse()
        {
            _task = null;
            return _responseStream.Current;
        }
    }


    public interface IDataStreamingService<TRequest, TRequestBatch, TResponse> : IDisposable
        where TRequest : IStreamingModel
    {
        bool IsServiceAvailable { get; }
        bool IsServiceEnabled { get; }
        bool IsStreaming { get; }
        string EndpointHost { get; }
        int EndpointPort { get; }
        bool EndpointSsl { get; }
        int BatchSizeConfigValue { get; }
        float? EndpointTestFlaky { get; }
        int? EndpointTestDelayMs { get; }
        int TimeoutConnectMs { get; }
        int TimeoutSendDataMs { get; }
        void Shutdown(bool withRestart);
        void StartConsumingCollection(PartitionedBlockingCollection<TRequest> collection);
        void Wait(int millisecondsTimeout = -1);

        bool ReadAndValidateConfiguration();
    }

    public abstract class DataStreamingService<TRequest, TRequestBatch, TResponse> : IDataStreamingService<TRequest, TRequestBatch, TResponse>
        where TRequest : class, IStreamingModel
        where TRequestBatch : class, IStreamingBatchModel<TRequest>
    {
        private const string UnimplementedStatus = "UNIMPLEMENTED";
        private const string UnavailableStatus = "UNAVAILABLE";
        private const string FailedPreconditionStatus = "FAILED_PRECONDITION";
        private const string InternalStatus = "INTERNAL";
        private const string OkStatus = "OK";
        private readonly IGrpcWrapper<TRequestBatch, TResponse> _grpcWrapper;
        private readonly IDelayer _delayer;
        protected readonly IAgentHealthReporter _agentHealthReporter;
        private readonly IAgentTimerService _agentTimerService;
        private readonly IEnvironment _environment;

        private const string LicenseKeyHeaderName = "license_key";

        private readonly IConfigurationService _configSvc;
        protected IConfiguration _configuration => _configSvc?.Configuration;

        private bool _hasAnyStreamStarted;
        public bool IsStreaming { get; private set; }

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

        private readonly InterlockedCounter _workCounter = new InterlockedCounter();

        protected Metadata _metadataHeaders;
        protected Metadata MetadataHeaders => _metadataHeaders;
        private Metadata CreateMetadataHeaders()
        {
            var headers = new Metadata();

            headers.Add(new Metadata.Entry("agent_run_token", _configuration.AgentRunId.ToString()));
            headers.Add(new Metadata.Entry(LicenseKeyHeaderName, _configuration.AgentLicenseKey));

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

            if (EndpointTestFlakyCode.HasValue)
            {
                headers.Add(new Metadata.Entry("flaky_code", EndpointTestFlakyCode.ToString()));
            }

            if (CompressionEnabled)
            {
                headers.Add("grpc-internal-encoding-request", "gzip");
            }

            if (Log.IsFinestEnabled)
            {
                var parametersString = string.Empty;
                foreach(var header in headers)
                {
                    if (parametersString.Length > 0)
                    {
                        parametersString += ",";
                    }
                    if (header.Key == LicenseKeyHeaderName)
                    {
                        var obfuscatedLicenseKey = Strings.ObfuscateLicenseKey(header.Value);
                        parametersString += $"{header.Key}={obfuscatedLicenseKey}";
                    }
                    else
                    {
                        parametersString += $"{header.Key}={header.Value}";
                    }
                }
                LogMessage(LogLevel.Finest, $"Creating gRPC Metadata ({parametersString})");
            }

            return headers;
        }

        private CancellationTokenSource _cancellationTokenSource;

        protected CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public string EndpointHost { get; private set; }
        public int EndpointPort { get; private set; }
        public bool EndpointSsl { get; private set; }
        public float? EndpointTestFlaky { get; private set; }
        public int? EndpointTestFlakyCode { get; private set; }
        public int? EndpointTestDelayMs { get; private set; }
        public abstract int BatchSizeConfigValue { get; }
        public bool CompressionEnabled { get; private set; }

        protected abstract string EndpointHostConfigValue { get; }
        protected abstract string EndpointPortConfigValue { get; }
        protected abstract string EndpointSslConfigValue { get; }
        protected abstract float? EndpointTestFlakyConfigValue { get; }
        protected abstract int? EndpointTestFlakyCodeConfigValue { get; }
        protected abstract int? EndpointTestDelayMsConfigValue { get; }


        protected abstract void HandleServerResponse(TResponse responseModel, int consumerId);

        protected abstract void RecordSuccessfulSend(int countItems);
        protected abstract void RecordGrpcError(string status);
        protected abstract void RecordResponseError();
        protected abstract void RecordSendTimeout();

        private volatile bool _shouldRestart = false;

        private bool? _isServiceEnabled;
        public bool IsServiceEnabled => _isServiceEnabled ?? (_isServiceEnabled = ReadAndValidateConfiguration()).Value;
        public bool IsServiceAvailable => IsServiceEnabled &&
            _grpcWrapper.IsConnected &&
            IsConfigurationValid &&
            _hasAnyStreamStarted;

        private PartitionedBlockingCollection<TRequest> _collection;

        protected DataStreamingService(IGrpcWrapper<TRequestBatch, TResponse> grpcWrapper, IDelayer delayer, IConfigurationService configSvc, IAgentHealthReporter agentHealthReporter, IAgentTimerService agentTimerService, IEnvironment environment)
        {
            _grpcWrapper = grpcWrapper;
            _delayer = delayer;
            _configSvc = configSvc;
            _agentTimerService = agentTimerService;
            _environment = environment;

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
            CompressionEnabled = _configuration.InfiniteTracingCompression;

            var configHost = EndpointHostConfigValue;
            var configPortStr = EndpointPortConfigValue;
            if (string.IsNullOrWhiteSpace(configPortStr))
            {
                configPortStr = "443";
            }
            var configSslStr = EndpointSslConfigValue;
            if (string.IsNullOrWhiteSpace(configSslStr))
            {
                configSslStr = "True";
            }

            //Infinite Tracing Disabled
            if (string.IsNullOrWhiteSpace(configHost))
            {
                EndpointHost = null;
                EndpointPort = -1;
                EndpointSsl = true;
                EndpointTestFlaky = null;
                EndpointTestFlakyCode = null;
                EndpointTestDelayMs = null;

                return false;
            }

            var isValidHost = Uri.CheckHostName(configHost) != UriHostNameType.Unknown;
            var isValidPort = int.TryParse(configPortStr, out var configPort) && configPort > 0 && configPort <= 65535;
            var isValidSsl = bool.TryParse(configSslStr, out var configSsl);
            var isValidFlaky = EndpointTestFlakyConfigValue == null || (EndpointTestFlakyConfigValue >= 0 && EndpointTestFlakyConfigValue <= 100);
            var isValidFlakyCode = EndpointTestFlakyCodeConfigValue == null || (EndpointTestFlakyCodeConfigValue >= 0 && EndpointTestFlakyCodeConfigValue <= 16); // See https://github.com/grpc/grpc/blob/master/doc/statuscodes.md
            var isValidDelay = EndpointTestDelayMsConfigValue == null || (EndpointTestDelayMsConfigValue >= 0);
            var isValidTimeoutConnect = TimeoutConnectMs > 0;
            var isValidTimeoutSend = TimeoutSendDataMs > 0;
            var isValidBatchSize = BatchSizeConfigValue > 0;

            if (isValidHost && isValidPort && isValidSsl && isValidFlaky && isValidFlakyCode && isValidDelay && isValidTimeoutConnect && isValidTimeoutSend && isValidBatchSize)
            {
                EndpointHost = configHost;
                EndpointPort = configPort;
                EndpointSsl = configSsl;
                EndpointTestFlaky = EndpointTestFlakyConfigValue;
                EndpointTestFlakyCode = EndpointTestFlakyCodeConfigValue;
                EndpointTestDelayMs = EndpointTestDelayMsConfigValue;

                CheckForLegacyProxyAndDisplayWarning();

                return true;
            }

            EndpointHost = null;
            EndpointPort = -1;
            EndpointSsl = true;
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

            if (!isValidSsl)
            {
                LogMessage(LogLevel.Info, $"Invalid Configuration.  Endpoint SSL '{configSslStr}' is not valid.  Infinite Tracing will NOT be started.");
            }

            if (!isValidFlaky)
            {
                LogMessage(LogLevel.Info, $"Invalid Configuration For Test.  Flaky % '{EndpointTestFlakyConfigValue}' is not valid.  Infinite Tracing will NOT be started.");
            }

            if (!isValidFlakyCode)
            {
                LogMessage(LogLevel.Info, $"Invalid Configuration For Test.  Flaky response code '{EndpointTestFlakyCodeConfigValue}' is not valid.  Infinite Tracing will NOT be started.");
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

            if (!isValidBatchSize)
            {
                LogMessage(LogLevel.Info, $"Invalid Configuration. Batch Size '{BatchSizeConfigValue}' is not valid.  Infinite Tracing will NOT be started.");
            }

            return false;
        }

        private void CheckForLegacyProxyAndDisplayWarning()
        {
            var grpcProxyValue = _environment.GetEnvironmentVariable("grpc_proxy");
            var httpsProxyValue = _environment.GetEnvironmentVariable("https_proxy");

            if (!string.IsNullOrWhiteSpace(grpcProxyValue) && string.IsNullOrWhiteSpace(httpsProxyValue))
            {
                LogMessage(LogLevel.Warn, "The 'grpc_proxy' environment variable is deprecated. Please use 'https_proxy' instead.");
            }
        }

        private void Restart(PartitionedBlockingCollection<TRequest> collection)
        {
            _grpcWrapper.Shutdown();

            _collection = collection;

            do
            {
                _shouldRestart = false;

                if (StartService())
                {
                    StartConsumers(); //This blocks until the cancellation token is triggered
                }
            } while (_shouldRestart);
        }

        private readonly ConcurrentDictionary<int, ResponseStreamWrapper<TResponse>> _responseStreamsDic = new ConcurrentDictionary<int, ResponseStreamWrapper<TResponse>>();

        private static readonly TimeSpan _responseStreamResponseInterval = TimeSpan.FromSeconds(2);

        private void ManageResponseStreams(CancellationToken serviceCancellationToken)
        {
            while (!serviceCancellationToken.IsCancellationRequested)
            {
                //Remove anything that should not be there
                foreach (var x in _responseStreamsDic.Values.Where(x => x.IsInvalid).ToList())
                {
                    LogMessage(LogLevel.Finest, x.ConsumerID, "Response Stream Manager - Removing Stream");
                    _responseStreamsDic.TryRemove(x.ConsumerID, out _);
                    if ((x.ResponseRpcException != null) && (x.ResponseRpcException.StatusCode == StatusCode.FailedPrecondition))
                    {
                        LogMessage(LogLevel.Debug, $"The gRPC endpoint defined at {EndpointHost}:{EndpointPort} returned {FailedPreconditionStatus}, indicating the traffic is being redirected to a new host.  Restarting service.");
                        Shutdown(true);
                    }
                }

                var tasksToWaitFor = _responseStreamsDic.Values
                    .Where(x => !x.IsInvalid)
                    .Select(x => x.GetAwaiter())
                    .ToArray();

                if (tasksToWaitFor.Length == 0)
                {
                    Thread.Sleep(_responseStreamResponseInterval);
                    continue;
                }

                //Wait for task to complete or expire after interval window
                var taskIdx = Task.WaitAny(tasksToWaitFor, _responseStreamResponseInterval);

                //Service is being shutdown
                if (serviceCancellationToken.IsCancellationRequested)
                {
                    LogMessage(LogLevel.Debug, "Response Stream Manager - Shutting Down");
                    break;
                }

                //None of the tasks completed, no results to process
                if (taskIdx < 0)
                {
                    continue;
                }

                var task = tasksToWaitFor[taskIdx];

                if (task.IsFaulted)
                {
                    LogMessage(LogLevel.Debug, $"Response Stream Manager - Task {taskIdx} Faulted", task.Exception);
                    continue;
                }

                var consumerId = task.Result;

                if (_responseStreamsDic.TryGetValue(consumerId, out var responseStreamInfo))
                {
                    if (responseStreamInfo.IsInvalid)
                    {
                        LogMessage(LogLevel.Finest, consumerId, "Response Stream has been marked invalid");
                    }
                    else
                    {
                        var responseMsg = responseStreamInfo.RetrieveResponse();
                        HandleServerResponse(responseMsg, consumerId);
                    }
                }
            }

            _responseStreamsDic.Clear();
        }

        private void StartConsumers()
        {
            _hasAnyStreamStarted = false;
            IsStreaming = false;

            //Check to make sure that we actually connected to the grpcService and that
            //streaming is enabled
            if (!_grpcWrapper.IsConnected)
            {
                return;
            }

            _responseStreamsDic.Clear();
            Task.Run(() => ManageResponseStreams(CancellationToken));

            //Start up the workers
            for (var i = 0; i < _configuration.InfiniteTracingTraceCountConsumers; i++)
            {
                Task.Run(() => ExecuteConsumer(_collection));
            }

            CancellationToken.WaitHandle.WaitOne();

            _hasAnyStreamStarted = false;
            IsStreaming = false;
        }

        private bool StartService()
        {
            _isConfigurationValid = ReadAndValidateConfiguration();

            if (!IsServiceEnabled || !IsConfigurationValid)
            {
                return false;
            }

            LogConfigurationSettings();

            var metadataHeaders = CreateMetadataHeaders();

            Interlocked.Exchange(ref _metadataHeaders, metadataHeaders);

            _cancellationTokenSource = new CancellationTokenSource();

            return CreateChannel(_cancellationTokenSource.Token);
        }

        private void LogConfigurationSettings()
        {
            LogMessage(LogLevel.Info, $"Configuration Setting - Host - {EndpointHost}");
            LogMessage(LogLevel.Info, $"Configuration Setting - Port - {EndpointPort}");
            LogMessage(LogLevel.Finest, $"Configuration Setting - SSL - {EndpointSsl}");
            LogMessage(LogLevel.Info, $"Configuration Setting - Compression - {CompressionEnabled}");
            LogMessage(LogLevel.Info, $"Configuration Setting - Consumers - {_configuration.InfiniteTracingTraceCountConsumers}");
            LogMessage(LogLevel.Finest, $"Configuration Setting - Test Flaky - {EndpointTestFlaky?.ToString() ?? "NULL"}");
            LogMessage(LogLevel.Finest, $"Configuration Setting - Test Flaky Code - {EndpointTestFlakyCode?.ToString() ?? "NULL"}");
            LogMessage(LogLevel.Finest, $"Configuration Setting - Test Delay (ms) - {EndpointTestDelayMs?.ToString() ?? "NULL"}");
        }

        private bool CreateChannel(CancellationToken cancellationToken)
        {
            var attemptId = 0;

            var endpointIpAddr = GetIpAddressFromHostname(EndpointHost);

            LogMessage(LogLevel.Info, $"Creating gRPC channel to endpoint {EndpointHost}:{EndpointPort} (IP Address: {endpointIpAddr}).");

            while (!cancellationToken.IsCancellationRequested && IsServiceEnabled)
            {
                try
                {
                    var createdChannel = false;
                    using (_agentTimerService.StartNew(_timerEventNameForChannel))
                    {
                        createdChannel = _grpcWrapper.CreateChannel(EndpointHost, EndpointPort, EndpointSsl, MetadataHeaders, TimeoutConnectMs, cancellationToken);
                    }

                    if (createdChannel)
                    {
                        LogMessage(LogLevel.Info, $"gRPC channel to endpoint {EndpointHost}:{EndpointPort} connected. (attempt {attemptId})");
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
                    // **IMPORTANT** This error handling code will not be encountered for grpc-dotnet since there is no way to connect without having real data to send
                    RecordResponseError();
                    LogMessage(LogLevel.Debug, $"Error creating gRPC channel to endpoint {EndpointHost}:{EndpointPort}. (attempt {attemptId})", ex);

                    var grpcWrapperEx = ex as GrpcWrapperException;
                    if (grpcWrapperEx != null && !string.IsNullOrWhiteSpace(grpcWrapperEx.Status))
                    {
                        RecordGrpcError(grpcWrapperEx.Status);

                        if (grpcWrapperEx.Status == UnimplementedStatus)
                        {
                            LogMessage(LogLevel.Error, $"The gRPC endpoint defined at {EndpointHost}:{EndpointPort} is not available and no reconnection attempts will be made.");
                            Shutdown(false);
                            return false;
                        }

                        if (grpcWrapperEx.Status == FailedPreconditionStatus)
                        {
                            LogMessage(LogLevel.Error, $"The gRPC endpoint defined at {EndpointHost}:{EndpointPort} returned {FailedPreconditionStatus}, indicating the traffic is being redirected to a new host.  Restarting service.");
                            Shutdown(true);
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

        private string GetIpAddressFromHostname(string endpointHost)
        {
            var ipAddressString = "unknown";
            try
            {
                ipAddressString = Dns.GetHostAddresses(EndpointHost)[0].ToString();
            }
            catch (Exception)
            {
                // just swallow any exceptions and return the "unknown" string
            }
            return ipAddressString;

        }

        public void Dispose()
        {
            try
            {
                Shutdown(false);
            }
            catch (Exception ex)
            {
                LogMessage(LogLevel.Finest, $"Exception during dispose", ex);
            }
        }

        public void Shutdown(bool withRestart)
        {
            LogMessage(LogLevel.Debug, $"Shutdown Request Received, restart = {withRestart}");

            _shouldRestart = withRestart;
            _cancellationTokenSource.Cancel();
            _grpcWrapper.Shutdown();
        }

        private bool GetRequestStreamWithRetry(int consumerId, CancellationToken cancellationToken, out IClientStreamWriter<TRequestBatch> requestStream, out IAsyncStreamReader<TResponse> responseStream)
        {
            var attemptId = 0;

            requestStream = null;
            responseStream = null;

            LogMessage(LogLevel.Finest, consumerId, $"Creating gRPC request stream (attempt {attemptId}).");

            while (!cancellationToken.IsCancellationRequested && IsServiceEnabled)
            {
                var shouldRetryImmediately = false;

                try
                {
                    using (_agentTimerService.StartNew(_timerEventNameForStream))
                    {
                        if (_grpcWrapper.CreateStreams(MetadataHeaders, TimeoutConnectMs, cancellationToken, out requestStream, out responseStream))
                        {
                            LogMessage(LogLevel.Finest, consumerId, $"gRPC request stream connected (attempt {attemptId}).");
                            return true;
                        }
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    // **IMPORTANT** None of this error handling logic is hit for grpc-dotnet since we fake out creating/testing streams
                    RecordResponseError();
                    LogMessage(LogLevel.Debug, consumerId, $"Error creating gRPC request stream. (attempt {attemptId})", ex);

                    var grpcWrapperEx = ex as GrpcWrapperException;
                    if (grpcWrapperEx != null && !string.IsNullOrWhiteSpace(grpcWrapperEx.Status))
                    {
                        RecordGrpcError(grpcWrapperEx.Status);

                        if (grpcWrapperEx.Status == UnimplementedStatus)
                        {
                            LogMessage(LogLevel.Error, consumerId, $"The gRPC request stream could not be created because the gRPC endpoint defined at {EndpointHost}:{EndpointPort} is no longer available and no reconnection attempts will be made.");
                            Shutdown(false);
                            return false;
                        }

                        if (grpcWrapperEx.Status == UnavailableStatus)
                        {
                            LogMessage(LogLevel.Error, consumerId, $"The gRPC request stream could not be created because the gRPC endpoint defined at {EndpointHost}:{EndpointPort} is temporarily unavailable, so we will restart this service.");
                            Shutdown(true);
                            return false;
                        }

                        if (grpcWrapperEx.Status == FailedPreconditionStatus)
                        {
                            LogMessage(LogLevel.Error, consumerId, $"The gRPC request stream could not be created because the gRPC endpoint defined at {EndpointHost}:{EndpointPort} has been moved to a different host, so we will restart this service.");
                            Shutdown(true);
                            return false;
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

            return false;
        }

        public void Wait(int millisecondsTimeout)
        {
            LogMessage(LogLevel.Debug, $"DataStreamingService: Waiting up to {millisecondsTimeout} milliseconds for workers to finish streaming data...");

            var task = Task.Run(async () =>
            {
                // Wait until there are both no spans to be sent and no workers pending. Performance ?????
                while (_collection?.Count > 0 || _workCounter?.Value > 0)
                {
                    await Task.Delay(100);
                }
            });

            if (task.Wait(TimeSpan.FromMilliseconds(millisecondsTimeout)))
            {
                LogMessage(LogLevel.Debug, $"DataStreamingService: Finished streaming span data on exit.");
            }
            else
            {
                LogMessage(LogLevel.Debug, $"DataStreamingService: Could not finish streaming span data on exit: {_collection?.Count} span events need to be sent, {_workCounter?.Value} streaming workers are pending.");
            }
        }

        /// <summary>
        /// Designed to be called by the aggregator.
        /// </summary>
        /// <param name="collection"></param>
        public void StartConsumingCollection(PartitionedBlockingCollection<TRequest> collection)
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
        private void ExecuteConsumer(PartitionedBlockingCollection<TRequest> collection)
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
                    LogMessage(LogLevel.Finest, "Received an exception while attempting to StreamRequests.", ex);
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    var delayInMs = shouldRetryImmediately ? 0 : _delayBetweenRpcCallsMs;
                    LogMessage(LogLevel.Finest, "Delay " + delayInMs);
                    _delayer.Delay(delayInMs, cancellationToken);
                }
            }
        }

        private bool DequeueItems(PartitionedBlockingCollection<TRequest> collection, int maxBatchSize, CancellationToken cancellationToken, out IList<TRequest> items)
        {
            items = null;
            if (!collection.Take(out var firstItem, cancellationToken))
            {
                return false;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            items = new List<TRequest>();
            items.Add(firstItem);

            for (var i = 0; i < maxBatchSize - 1 && !cancellationToken.IsCancellationRequested && collection.TryTake(out var item); i++)
            {
                items.Add(item);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                ProcessFailedItems(items, collection);
                return false;
            }

            return true;
        }

        protected abstract TRequestBatch CreateBatch(IList<TRequest> items);

        private void ProcessFailedItems(IList<TRequest> items, PartitionedBlockingCollection<TRequest> collection)
        {
            foreach (var item in items)
            {
                if (!collection.TryAdd(item))
                {
                    _agentHealthReporter.ReportInfiniteTracingSpanEventsDropped(1);
                }
            }
        }

        private bool StreamRequests(PartitionedBlockingCollection<TRequest> collection, CancellationToken serviceCancellationToken)
        {
            var consumerId = _consumerId.Increment();
            using (var streamCancellationTokenSource = new CancellationTokenSource())
            using (var serviceAndStreamCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(serviceCancellationToken, streamCancellationTokenSource.Token))
            {
                if (!GetRequestStreamWithRetry(consumerId, serviceAndStreamCancellationTokenSource.Token, out var requestStream, out var responseStream))
                {
                    LogMessage(LogLevel.Debug, consumerId, "Unable to obtain Stream, exiting consumer");
                    streamCancellationTokenSource.Cancel();
                    return false;
                }

                _hasAnyStreamStarted = true;

                _responseStreamsDic[consumerId] = new ResponseStreamWrapper<TResponse>(consumerId, responseStream, serviceAndStreamCancellationTokenSource.Token, _agentHealthReporter);

                while (!serviceCancellationToken.IsCancellationRequested && _grpcWrapper.IsConnected)
                {

                    if (!DequeueItems(collection, BatchSizeConfigValue, serviceCancellationToken, out var items))
                    {
                        return false;
                    }

                    if (items == null || items.Count == 0)
                    {
                        LogMessage(LogLevel.Debug, consumerId, $"Expected a {_modelType} from the collection, but it was null");
                        continue;
                    }

                    _workCounter.Increment();

                    var trySendStatus = TrySend(consumerId, requestStream, items, serviceCancellationToken);

                    _workCounter.Decrement();

                    if (trySendStatus != TrySendStatus.Success)
                    {
                        ProcessFailedItems(items, collection);

                        _grpcWrapper.TryCloseRequestStream(requestStream);
                        streamCancellationTokenSource.Cancel();

                        return trySendStatus == TrySendStatus.ErrorWithImmediateRetry;
                    }

                    IsStreaming = true;
                }

                streamCancellationTokenSource.Cancel();
            }

            return false;
        }

        private TrySendStatus TrySend(int consumerId, IClientStreamWriter<TRequestBatch> requestStream, IList<TRequest> items, CancellationToken cancellationToken)
        {
            //If there is no channel, return
            if (cancellationToken.IsCancellationRequested)
            {
                return TrySendStatus.CancellationRequested;
            }

            LogMessage(LogLevel.Finest, consumerId, $"Attempting to send {items.Count} item(s).");

            var batch = CreateBatch(items);

            try
            {
                var sentData = false;
                using (_agentTimerService.StartNew(_timerEventNameForSend))
                {
                    sentData = _grpcWrapper.TrySendData(requestStream, batch, TimeoutSendDataMs, cancellationToken);
                }

                if (sentData)
                {
                    LogMessage(LogLevel.Finest, consumerId, $"Attempting to send {items.Count} item(s) - Success");
                    RecordSuccessfulSend(items.Count);

                    return TrySendStatus.Success;
                }

                RecordSendTimeout();
                LogMessage(LogLevel.Finest, consumerId, $"Attempting to send {items.Count} item(s) - Timed Out");
            }
            catch (GrpcWrapperStreamNotAvailableException streamNotAvailEx)
            {
                RecordResponseError();
                LogMessage(LogLevel.Finest, consumerId, $"Attempting to send {items.Count} item(s) - Request stream closed.", streamNotAvailEx);
            }
            catch (GrpcWrapperException grpcEx) when (!string.IsNullOrWhiteSpace(grpcEx.Status))
            {
                // **IMPORTANT** Grpc.Core error handling has two other layers of exception handling to catch connectivity errors, but Grpc.Net experiences connection errors in this handler
                RecordResponseError();
                RecordGrpcError(grpcEx.Status);
                var rpcEx = grpcEx.InnerException as RpcException;
                switch (grpcEx.Status)
                {
                    case OkStatus:
                        LogMessage(LogLevel.Finest, consumerId, $"Attempting to send {items.Count} item(s) - A stream was closed due to connection rebalance. New stream requested and data will be resent immediately.");
                        return TrySendStatus.ErrorWithImmediateRetry;
                    case UnimplementedStatus:
                        LogMessage(LogLevel.Error, consumerId, $"Attempting to send {items.Count} item(s) - Trace observer is no longer available, shutting down infinite tracing service.", (Exception)rpcEx ?? grpcEx);
                        Shutdown(false);
                        return TrySendStatus.Error;
                    case FailedPreconditionStatus:
                        LogMessage(LogLevel.Debug, consumerId, $"Attempting to send {items.Count} item(s) - Channel has been moved, requesting restart", (Exception)rpcEx ?? grpcEx);
                        Shutdown(true);
                        return TrySendStatus.Error;
                    case UnavailableStatus:
                        // **Important** this can be a TCP or GRPC layer error, but we don't currently have a way to tell the difference. This restarts everything anyways so it's not so important
                        LogMessage(LogLevel.Info, consumerId, $"Attempting to send {items.Count} item(s) - Channel not available, requesting restart", (Exception)rpcEx ?? grpcEx);
                        Shutdown(true);
                        return TrySendStatus.Error;
                    case InternalStatus:
                        // **Important** this can be a TCP or GRPC layer error, but we don't currently have a way to tell the difference so fall through to generic error handling
                    default:
                        LogMessage(LogLevel.Finest, consumerId, $"Attempting to send {items.Count} item(s)", (Exception)rpcEx ?? grpcEx);
                        break;
                }
            }
            catch (Exception ex)
            {
                RecordResponseError();
                LogMessage(LogLevel.Debug, consumerId, $"Unknown exception attempting to send {items.Count} item(s)", ex);
            }

            return TrySendStatus.Error;
        }

        protected void LogMessage(LogLevel level, string message, Exception ex = null)
        {
            Log.LogMessage(level, ex, $"{GetType().Name}: {message}");
        }

        protected void LogMessage(LogLevel level, int consumerId, string message, Exception ex = null)
        {
            LogMessage(level, $"consumer {consumerId} - {message}", ex);
        }

        protected void LogMessage(LogLevel level, int consumerId, TRequest item, string message, Exception ex = null)
        {
            LogMessage(level, consumerId, $"{_modelType} {item.DisplayName} - {message}", ex);
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
