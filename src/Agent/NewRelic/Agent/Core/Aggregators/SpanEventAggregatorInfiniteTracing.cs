// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Collections;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Aggregators;

public interface ISpanEventAggregatorInfiniteTracing
{
    void Collect(ISpanEventWireModel wireModel);
    void Collect(IEnumerable<ISpanEventWireModel> wireModels);
    bool IsServiceEnabled { get; }
    bool IsServiceAvailable { get; }
    bool HasCapacity(int proposedItems);
    void RecordDroppedSpans(int countDroppedSpans);
    void RecordSeenSpans(int countSeenSpans);
    void ReportSupportabilityMetrics();
    int Capacity { get; }
}

public class SpanEventAggregatorInfiniteTracing : DisposableService, ISpanEventAggregatorInfiniteTracing
{
    private PartitionedBlockingCollection<Span> _spanEvents;
    private readonly IDataStreamingService<Span, SpanBatch, RecordStatus> _spanStreamingService;
    private readonly IAgentHealthReporter _agentHealthReporter;
    private readonly IConfigurationService _configSvc;
    private readonly IScheduler _schedulerSvc;

    private IConfiguration _configuration => _configSvc?.Configuration;

    public int Capacity => (_spanEvents?.Capacity).GetValueOrDefault(0);

    public SpanEventAggregatorInfiniteTracing(IDataStreamingService<Span, SpanBatch, RecordStatus> spanStreamingService, IConfigurationService configSvc, IAgentHealthReporter agentHealthReporter, IScheduler scheduler)
    {   
        _spanStreamingService = spanStreamingService;
        _subscriptions.Add<AgentConnectedEvent>(AgentConnected);
        _subscriptions.Add<PreCleanShutdownEvent>(OnPreCleanShutdown);
        _agentHealthReporter = agentHealthReporter;
        _configSvc = configSvc;
        _schedulerSvc = scheduler;
    }

    /// <summary>
    /// This executes every time a local configuration change is made.  It is more convenient
    /// that OnConfigurationChanged
    /// </summary>
    /// <param name="_"></param>
    private void AgentConnected(AgentConnectedEvent _)
    {
        _spanStreamingService.Shutdown(false);
        _schedulerSvc.StopExecuting(ReportSupportabilityMetrics);

        var oldCapacity = (_spanEvents?.Capacity).GetValueOrDefault(0);
        var oldPartitionCount = (_spanEvents?.PartitionCount).GetValueOrDefault(0);
        var oldCount = (_spanEvents?.Count).GetValueOrDefault(0);

        var oldCollection = _spanEvents != null
            ? Interlocked.Exchange(ref _spanEvents, null)
            : null;

        var newCapacity = _configuration.InfiniteTracingQueueSizeSpans;
        var newPartitionCount = Math.Min(_configuration.InfiniteTracingQueueSizeSpans, _configuration.InfiniteTracingPartitionCountSpans);

        if (!IsServiceEnabled || newCapacity <= 0 || newPartitionCount <= 0 || newPartitionCount > 62)
        {
            if (oldCount > 0)
            {
                RecordDroppedSpans(oldCount);
            }

            if(IsServiceEnabled)
            {

                Log.Info($"SpanEventAggregatorInfiniteTracing: Configuration is invalid - Infinite Tracing will NOT be enabled.");
                LogConfiguration();
            }

            return;
        }

        if (oldCapacity == newCapacity && oldPartitionCount == newPartitionCount && oldCollection != null)
        {
            _spanEvents = oldCollection;
        }
        else
        {
            var overflowCount = oldCount - newCapacity;
            if (overflowCount > 0)
            {
                RecordDroppedSpans(overflowCount);
            }

            _spanEvents = oldCollection != null
                ? new PartitionedBlockingCollection<Span>(newCapacity, newPartitionCount, oldCollection)
                : new PartitionedBlockingCollection<Span>(newCapacity, newPartitionCount);
        }

        LogConfiguration();

        _schedulerSvc.ExecuteEvery(ReportSupportabilityMetrics, TimeSpan.FromMinutes(1));
        _spanStreamingService.StartConsumingCollection(_spanEvents);
    }

    private void OnPreCleanShutdown(PreCleanShutdownEvent obj)
    {
        if (_configuration.CollectorSendDataOnExit)
        {
            _spanStreamingService.Wait(_configuration.InfiniteTracingExitTimeoutMs);
        }

        return;
    }

    public void ReportSupportabilityMetrics()
    {
        _agentHealthReporter.ReportInfiniteTracingSpanQueueSize(_spanEvents.Count);
    }

    /// <summary>
    /// Allows the transformer to check to see if the aggregator has the capacity
    /// to store a proposed number of items.  If the streaming service has not been
    /// able to stream an item, set the capacity at 10% to avoid building up a large
    /// backlog of items during the time it takes to connect.
    /// </summary>
    /// <param name="proposedItems"></param>
    /// <returns></returns>
    public bool HasCapacity(int proposedItems)
    {
        var capacityFactor = _spanStreamingService.IsStreaming
            ? .9
            : .1;

        return _spanEvents != null && (_spanEvents.Count + proposedItems) < (Capacity * capacityFactor);
    }

    private void LogConfiguration()
    {
        Log.Info($"SpanEventAggregatorInfiniteTracing: Configuration Setting - Queue Size - {_configuration.InfiniteTracingQueueSizeSpans}");
        Log.Info($"SpanEventAggregatorInfiniteTracing: Configuration Setting - Queue Partitions - {_configuration.InfiniteTracingPartitionCountSpans}");
    }

    public bool IsServiceEnabled => _configuration.SpanEventsEnabled
                                    && _configuration.DistributedTracingEnabled
                                    && _spanStreamingService.IsServiceEnabled;
    public bool IsServiceAvailable => IsServiceEnabled && _spanEvents != null && _spanStreamingService.IsServiceAvailable;

    public void RecordDroppedSpans(int countDroppedSpans)
    {
        _agentHealthReporter.ReportInfiniteTracingSpanEventsDropped(countDroppedSpans);
    }

    public void RecordSeenSpans(int countSeenSpans)
    {
        _agentHealthReporter.ReportInfiniteTracingSpanEventsSeen(countSeenSpans);
    }

    public void Collect(ISpanEventWireModel wireModel)
    {
        RecordSeenSpans(1);

        if (_spanEvents == null || !_spanEvents.TryAdd(wireModel.Span))
        {
            RecordDroppedSpans(1);
        }
    }

    public void Collect(IEnumerable<ISpanEventWireModel> wireModels)
    {
        if (_spanEvents == null)
        {
            var countSpans = wireModels.Count();

            _agentHealthReporter.ReportInfiniteTracingSpanEventsSeen(countSpans);
            RecordDroppedSpans(countSpans);

            return;
        }

        foreach (var span in wireModels)
        {
            Collect(span);
        }
    }
}
