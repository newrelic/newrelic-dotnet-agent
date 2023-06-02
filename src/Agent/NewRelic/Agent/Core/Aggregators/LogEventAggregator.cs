// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Collections;
using NewRelic.Core.Logging;
using NewRelic.SystemInterfaces;

namespace NewRelic.Agent.Core.Aggregators
{
    public interface ILogEventAggregator
    {
        void Collect(LogEventWireModel loggingEventWireModel);

        void CollectWithPriority(IList<LogEventWireModel> logEventWireModels, float priority);
    }

    /// <summary>
    /// An service for collecting and managing logging events.
    /// </summary>
    public class LogEventAggregator : AbstractAggregator<LogEventWireModel>, ILogEventAggregator
    {
        private const double ReservoirReductionSizeMultiplier = 0.5;

        private readonly IAgentHealthReporter _agentHealthReporter;

        private ConcurrentPriorityQueue<PrioritizedNode<LogEventWireModel>> _logEvents = new ConcurrentPriorityQueue<PrioritizedNode<LogEventWireModel>>(0);
        private int _logsDroppedCount;

        public LogEventAggregator(IDataTransportService dataTransportService, IScheduler scheduler, IProcessStatic processStatic, IAgentHealthReporter agentHealthReporter)
            : base(dataTransportService, scheduler, processStatic)
        {
            _agentHealthReporter = agentHealthReporter;
            ResetCollections(_configuration.LogEventsMaxSamplesStored);
        }

        protected override TimeSpan HarvestCycle => _configuration.LogEventsHarvestCycle;
        protected override bool IsEnabled => _configuration.LogEventCollectorEnabled;

        public override void Dispose()
        {
            base.Dispose();
        }

        public override void Collect(LogEventWireModel loggingEventWireModel)
        {
            _agentHealthReporter.ReportLoggingEventCollected();
            AddEventToCollection(loggingEventWireModel);
        }

        public void CollectWithPriority(IList<LogEventWireModel> logEventWireModels, float priority)
        {
            for (int i = 0; i < logEventWireModels.Count; i++)
            {
                _agentHealthReporter.ReportLoggingEventCollected();
                logEventWireModels[i].Priority = priority;
                AddEventToCollection(logEventWireModels[i]);
            }
        }

        protected override async Task HarvestAsync()
        {
            Log.Finest("Log Event harvest starting.");

            var originalLogEvents = GetAndResetLogEvents(GetReservoirSize());
            var aggregatedEvents = originalLogEvents.Where(node => node != null).Select(node => node.Data).ToList();

            // Retrieve the number of add attempts before resetting the collection.
            var eventHarvestData = new EventHarvestData(originalLogEvents.Size, originalLogEvents.GetAddAttemptsCount());

            // increment the count of logs dropped since we last reported
            Interlocked.Add(ref _logsDroppedCount, originalLogEvents.GetAndResetDroppedItemCount());

            // if we don't have any events to publish then don't
            if (aggregatedEvents.Count <= 0)
            {
                return;
            }

            // matches metadata so that utilization and this match
            var hostname = !string.IsNullOrEmpty(_configuration.UtilizationFullHostName)
                ? _configuration.UtilizationFullHostName
                : _configuration.UtilizationHostName;

            var modelsCollection = new LogEventWireModelCollection(
                _configuration.ApplicationNames.ElementAt(0),
                _configuration.EntityGuid,
                hostname,
                aggregatedEvents);

            var responseStatus = await DataTransportService.SendAsync(modelsCollection);

            HandleResponse(responseStatus, aggregatedEvents);

            Log.Finest("Log Event harvest finished.");
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
            // If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).

            ResetCollections(_configuration.LogEventsMaxSamplesStored);
        }

        #region Private Helpers

        private void ResetCollections(int logEventCollectionCapacity)
        {
            GetAndResetLogEvents(logEventCollectionCapacity);
        }

        private ConcurrentPriorityQueue<PrioritizedNode<LogEventWireModel>> GetAndResetLogEvents(int logEventCollectionCapacity)
        {
            return Interlocked.Exchange(ref _logEvents, new ConcurrentPriorityQueue<PrioritizedNode<LogEventWireModel>>(logEventCollectionCapacity));
        }

        private void AddEventToCollection(LogEventWireModel logEventWireModel)
        {
            _logEvents.Add(new PrioritizedNode<LogEventWireModel>(logEventWireModel));
        }

        private int GetReservoirSize()
        {
            return _logEvents.Size;
        }

        private void ReduceReservoirSize(int newSize)
        {
            if (newSize >= GetReservoirSize())
                return;

            _logEvents.Resize(newSize);
        }

        private void HandleResponse(DataTransportResponseStatus responseStatus, ICollection<LogEventWireModel> logEvents)
        {
            switch (responseStatus)
            {
                case DataTransportResponseStatus.RequestSuccessful:
                    _agentHealthReporter.ReportLoggingEventsSent(logEvents.Count);
                    break;
                case DataTransportResponseStatus.Retain:
                    RetainEvents(logEvents);
                    break;
                case DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard:
                    ReduceReservoirSize((int)(logEvents.Count * ReservoirReductionSizeMultiplier));
                    RetainEvents(logEvents);
                    break;
                case DataTransportResponseStatus.Discard:
                default:
                    break;
            }

            // always report (and reset) the count of dropped logs, if any
            ReportDroppedLogCount();
        }

        private void ReportDroppedLogCount()
        {
            var droppedLogsCount = Interlocked.Exchange(ref _logsDroppedCount, 0);

            if (droppedLogsCount > 0)
            {
                _agentHealthReporter.ReportLoggingEventsDropped(droppedLogsCount);
            }

        }

        private void RetainEvents(IEnumerable<LogEventWireModel> logEvents)
        {
            logEvents = logEvents.ToList();

            foreach (var logEvent in logEvents)
            {
                if (logEvent != null)
                {
                    AddEventToCollection(logEvent);
                }
            }
        }

        #endregion
    }
}
