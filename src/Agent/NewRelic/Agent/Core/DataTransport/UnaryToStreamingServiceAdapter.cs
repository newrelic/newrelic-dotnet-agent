// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Extensions.Collections;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.DataTransport;

/// <summary>
/// TEMPORARY migration shim. Presents the unary <see cref="IUnaryDataService{TRequest, TRequestBatch, TResponse}"/>
/// through the streaming <see cref="IDataStreamingService{TRequest, TRequestBatch, TResponse}"/> interface so the
/// span aggregator can switch transports behind a feature toggle without being modified. The only behavioral
/// mapping is <c>IsStreaming</c> -&gt; <c>IsSending</c>; everything else delegates 1:1.
///
/// Remove this adapter (and the toggle in AgentServices) once the aggregator is repointed at
/// <see cref="IUnaryDataService{TRequest, TRequestBatch, TResponse}"/> and the streaming path is deleted.
/// </summary>
public class UnaryToStreamingServiceAdapter : IDataStreamingService<Span, SpanBatch, RecordStatus>
{
    private readonly IUnaryDataService<Span, SpanBatch, RecordStatus> _unaryService;

    public UnaryToStreamingServiceAdapter(IUnaryDataService<Span, SpanBatch, RecordStatus> unaryService)
    {
        _unaryService = unaryService;
        Log.Info("Infinite Tracing: using the unary span transport (NEW_RELIC_INFINITE_TRACING_USE_UNARY is enabled).");
    }

    public bool IsServiceAvailable => _unaryService.IsServiceAvailable;
    public bool IsServiceEnabled => _unaryService.IsServiceEnabled;

    // The streaming aggregator reads IsStreaming (e.g. in HasCapacity); the unary equivalent is IsSending.
    public bool IsStreaming => _unaryService.IsSending;

    public string EndpointHost => _unaryService.EndpointHost;
    public int EndpointPort => _unaryService.EndpointPort;
    public bool EndpointSsl => _unaryService.EndpointSsl;
    public int BatchSizeConfigValue => _unaryService.BatchSizeConfigValue;
    public float? EndpointTestFlaky => _unaryService.EndpointTestFlaky;
    public int? EndpointTestDelayMs => _unaryService.EndpointTestDelayMs;
    public int TimeoutConnectMs => _unaryService.TimeoutConnectMs;
    public int TimeoutSendDataMs => _unaryService.TimeoutSendDataMs;

    public void Shutdown(bool withRestart) => _unaryService.Shutdown(withRestart);

    public void StartConsumingCollection(PartitionedBlockingCollection<Span> collection) => _unaryService.StartConsumingCollection(collection);

    public void Wait(int millisecondsTimeout = -1) => _unaryService.Wait(millisecondsTimeout);

    public bool ReadAndValidateConfiguration() => _unaryService.ReadAndValidateConfiguration();

    public void Dispose() => _unaryService.Dispose();
}
