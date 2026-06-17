// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Collections;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.DataTransport;

public abstract class UnaryDataService<TRequest, TRequestBatch, TResponse> : IUnaryDataService<TRequest, TRequestBatch, TResponse>
    where TRequest : class, IStreamingModel
    where TRequestBatch : class, IStreamingBatchModel<TRequest>
{
    private const string UnimplementedStatus = "UNIMPLEMENTED";
    private const string UnavailableStatus = "UNAVAILABLE";
    private const string FailedPreconditionStatus = "FAILED_PRECONDITION";
    private const string InternalStatus = "INTERNAL";
    private const string DeadlineExceededStatus = "DEADLINE_EXCEEDED";

    private readonly IGrpcUnaryWrapper<TRequestBatch, TResponse> _grpcWrapper;
    private readonly IDelayer _delayer;
    protected readonly IAgentHealthReporter _agentHealthReporter;
    private readonly IAgentTimerService _agentTimerService;
    private readonly IEnvironment _environment;

    private const string LicenseKeyHeaderName = "license_key";

    private readonly IConfigurationService _configSvc;
    protected IConfiguration _configuration => _configSvc?.Configuration;

    private bool _hasAnyWorkerStarted;
    public bool IsSending { get; private set; }

    private readonly string _modelType = typeof(TRequest).Name;
    private readonly string _timerEventNameForSend = "gRPCSendUnary" + typeof(TRequest).Name;
    private readonly string _timerEventNameForChannel = "gRPCCreateChannelUnary" + typeof(TRequest).Name;

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
    /// worker threads running.
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
            foreach (var header in headers)
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
                                      _hasAnyWorkerStarted;

    private PartitionedBlockingCollection<TRequest> _collection;

    protected UnaryDataService(IGrpcUnaryWrapper<TRequestBatch, TResponse> grpcWrapper, IDelayer delayer, IConfigurationService configSvc, IAgentHealthReporter agentHealthReporter, IAgentTimerService agentTimerService, IEnvironment environment)
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

    private void StartConsumers()
    {
        _hasAnyWorkerStarted = false;
        IsSending = false;

        //Check to make sure that we actually connected to the grpcService
        if (!_grpcWrapper.IsConnected)
        {
            return;
        }

        //Start up the workers
        for (var i = 0; i < _configuration.InfiniteTracingTraceCountConsumers; i++)
        {
            Task.Run(() => ExecuteConsumer(_collection));
        }

        CancellationToken.WaitHandle.WaitOne();

        _hasAnyWorkerStarted = false;
        IsSending = false;
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
                    createdChannel = _grpcWrapper.CreateChannel(EndpointHost, EndpointPort, EndpointSsl, TimeoutConnectMs, cancellationToken);
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

    public void Wait(int millisecondsTimeout)
    {
        LogMessage(LogLevel.Debug, $"UnaryDataService: Waiting up to {millisecondsTimeout} milliseconds for workers to finish sending data...");

        var task = Task.Run(async () =>
        {
            // Wait until there are both no spans to be sent and no workers pending.
            while (_collection?.Count > 0 || _workCounter?.Value > 0)
            {
                await Task.Delay(100);
            }
        });

        if (task.Wait(TimeSpan.FromMilliseconds(millisecondsTimeout)))
        {
            LogMessage(LogLevel.Debug, $"UnaryDataService: Finished sending span data on exit.");
        }
        else
        {
            LogMessage(LogLevel.Debug, $"UnaryDataService: Could not finish sending span data on exit: {_collection?.Count} span events need to be sent, {_workCounter?.Value} sending workers are pending.");
        }
    }

    /// <summary>
    /// Designed to be called by the aggregator.
    /// </summary>
    public void StartConsumingCollection(PartitionedBlockingCollection<TRequest> collection)
    {
        if (collection == null)
        {
            Log.Debug("Unable to start Unary Data Service because queue was NULL.");
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
    private void ExecuteConsumer(PartitionedBlockingCollection<TRequest> collection)
    {
        var cancellationToken = CancellationToken;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                SendRequests(collection, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                LogMessage(LogLevel.Finest, "Received an exception while attempting to SendRequests.", ex);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                LogMessage(LogLevel.Finest, "Delay " + _delayBetweenRpcCallsMs);
                _delayer.Delay(_delayBetweenRpcCallsMs, cancellationToken);
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

    private void SendRequests(PartitionedBlockingCollection<TRequest> collection, CancellationToken serviceCancellationToken)
    {
        var consumerId = _consumerId.Increment();

        _hasAnyWorkerStarted = true;

        while (!serviceCancellationToken.IsCancellationRequested && _grpcWrapper.IsConnected)
        {
            if (!DequeueItems(collection, BatchSizeConfigValue, serviceCancellationToken, out var items))
            {
                return;
            }

            if (items == null || items.Count == 0)
            {
                LogMessage(LogLevel.Debug, consumerId, $"Expected a {_modelType} from the collection, but it was null");
                continue;
            }

            _workCounter.Increment();

            var trySendStatus = TrySend(consumerId, items, serviceCancellationToken);

            _workCounter.Decrement();

            if (trySendStatus != TrySendStatus.Success)
            {
                ProcessFailedItems(items, collection);
                return;
            }

            IsSending = true;
        }
    }

    private TrySendStatus TrySend(int consumerId, IList<TRequest> items, CancellationToken cancellationToken)
    {
        //If cancellation has been requested, return
        if (cancellationToken.IsCancellationRequested)
        {
            return TrySendStatus.CancellationRequested;
        }

        LogMessage(LogLevel.Finest, consumerId, $"Attempting to send {items.Count} item(s).");

        var batch = CreateBatch(items);

        try
        {
            var sentData = false;
            TResponse response;
            using (_agentTimerService.StartNew(_timerEventNameForSend))
            {
                sentData = _grpcWrapper.TrySendData(batch, MetadataHeaders, TimeoutSendDataMs, cancellationToken, out response);
            }

            if (sentData)
            {
                LogMessage(LogLevel.Finest, consumerId, $"Attempting to send {items.Count} item(s) - Success");
                RecordSuccessfulSend(items.Count);
                HandleServerResponse(response, consumerId);

                return TrySendStatus.Success;
            }

            // TrySendData returns false (without throwing) when the batch could not be sent at all -
            // cancellation was requested, or the channel was unavailable (e.g., during shutdown/restart).
            // Re-queue the items; the reconnect logic will recover.
            return TrySendStatus.CancellationRequested;
        }
        catch (GrpcWrapperException grpcEx) when (!string.IsNullOrWhiteSpace(grpcEx.Status))
        {
            // **Status -> action mapping.** Unary differs from streaming: there is no "OK = rebalance"
            // case, and DEADLINE_EXCEEDED is new (we set a per-call deadline). Refine as needed.
            RecordResponseError();
            RecordGrpcError(grpcEx.Status);
            var rpcEx = grpcEx.InnerException as RpcException;
            switch (grpcEx.Status)
            {
                case DeadlineExceededStatus:
                    RecordSendTimeout();
                    LogMessage(LogLevel.Finest, consumerId, $"Attempting to send {items.Count} item(s) - Timed Out");
                    break;
                case UnimplementedStatus:
                    LogMessage(LogLevel.Error, consumerId, $"Attempting to send {items.Count} item(s) - Trace observer is no longer available, shutting down infinite tracing service.", (Exception)rpcEx ?? grpcEx);
                    Shutdown(false);
                    return TrySendStatus.Error;
                case FailedPreconditionStatus:
                    LogMessage(LogLevel.Debug, consumerId, $"Attempting to send {items.Count} item(s) - Channel has been moved, requesting restart", (Exception)rpcEx ?? grpcEx);
                    Shutdown(true);
                    return TrySendStatus.Error;
                case UnavailableStatus:
                    // **Important** this can be a TCP or gRPC layer error; restarting recreates the channel.
                    LogMessage(LogLevel.Info, consumerId, $"Attempting to send {items.Count} item(s) - Channel not available, requesting restart", (Exception)rpcEx ?? grpcEx);
                    Shutdown(true);
                    return TrySendStatus.Error;
                case InternalStatus:
                // **Important** this can be a TCP or gRPC layer error, fall through to generic handling
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
        Error
    }
}
