// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Core.Logging;
using NewRelic.SystemInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.Aggregators
{
    public interface ISqlTraceAggregator : IDisposable
    {
        void Collect(SqlTraceStatsCollection sqlTrStats);
    }

    public class SqlTraceAggregator : AbstractAggregator<SqlTraceStatsCollection>, ISqlTraceAggregator
    {
        private SqlTraceStatsCollection _sqlTraceStats = new SqlTraceStatsCollection();

        private readonly object _sqlTraceLock = new object();

        private readonly IAgentHealthReporter _agentHealthReporter;

        public SqlTraceAggregator(IDataTransportService dataTransportService, IScheduler scheduler, IProcessStatic processStatic, IAgentHealthReporter agentHealthReporter)
            : base(dataTransportService, scheduler, processStatic)
        {
            _agentHealthReporter = agentHealthReporter;
        }

        protected override TimeSpan HarvestCycle => _configuration.SqlTracesHarvestCycle;

        protected override bool IsEnabled => _configuration.SlowSqlEnabled;

        public override void Collect(SqlTraceStatsCollection sqlTraceStats)
        {
            lock (_sqlTraceLock)
            {
                this._sqlTraceStats.Merge(sqlTraceStats);
            }
        }

        protected override void ManualHarvest(string transactionId) => InternalHarvest(transactionId);

        protected override void Harvest() => InternalHarvest();

        protected void InternalHarvest(string transactionId = null)
        {
            Log.Finest("SQL Trace harvest starting.");

            IDictionary<long, SqlTraceWireModel> oldSqlTraces;
            lock (_sqlTraceLock)
            {
                oldSqlTraces = _sqlTraceStats.Collection;
                _sqlTraceStats = new SqlTraceStatsCollection();
            }

            var slowestTraces = oldSqlTraces.Values
                .Where(trace => trace != null)
                .OrderByDescending(trace => trace.MaxCallTime)
                .Take(_configuration.SqlTracesPerPeriod)
                .ToList();

            if (!slowestTraces.Any())
                return;

            var responseStatus = DataTransportService.Send(slowestTraces, transactionId);

            HandleResponse(responseStatus, slowestTraces);

            Log.Finest("SQL Trace harvest finished.");
        }

        private void HandleResponse(DataTransportResponseStatus responseStatus, ICollection<SqlTraceWireModel> traces)
        {
            switch (responseStatus)
            {
                case DataTransportResponseStatus.RequestSuccessful:
                    _agentHealthReporter.ReportSqlTracesSent(traces.Count);
                    break;
                case DataTransportResponseStatus.Retain:
                    Retain(traces);
                    break;
                case DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard:
                case DataTransportResponseStatus.Discard:
                default:
                    break;
            }
        }

        private void Retain(ICollection<SqlTraceWireModel> traces)
        {
            _agentHealthReporter.ReportSqlTracesRecollected(traces.Count);

            var tracesCollection = new SqlTraceStatsCollection();

            foreach (var trace in traces)
            {
                tracesCollection.Insert(trace);
            }

            Collect(tracesCollection);
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
            // If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).

            lock (_sqlTraceLock)
            {
                _sqlTraceStats = new SqlTraceStatsCollection();
            }
        }
    }
}
