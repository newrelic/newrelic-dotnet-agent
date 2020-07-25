using System;
using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
using NewRelic.SystemInterfaces;

namespace NewRelic.Agent.Core.Aggregators
{
    public interface ISqlTraceAggregator
    {
        void Collect(SqlTraceStatsCollection sqlTrStats);
    }

    public class SqlTraceAggregator : AbstractAggregator<SqlTraceStatsCollection>, ISqlTraceAggregator
    {
        private SqlTraceStatsCollection _sqlTraceStats = new SqlTraceStatsCollection();
        private readonly Object _sqlTraceLock = new Object();
        private readonly IAgentHealthReporter _agentHealthReporter;

        public SqlTraceAggregator(IDataTransportService dataTransportService, IScheduler scheduler, IProcessStatic processStatic, IAgentHealthReporter agentHealthReporter)
            : base(dataTransportService, scheduler, processStatic)
        {
            _agentHealthReporter = agentHealthReporter;
        }

        public override void Collect(SqlTraceStatsCollection sqlTraceStats)
        {
            lock (_sqlTraceLock)
            {
                this._sqlTraceStats.Merge(sqlTraceStats);
            }
        }

        protected override void Harvest()
        {
            IDictionary<Int64, SqlTraceWireModel> oldSqlTraces;
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

            _agentHealthReporter.ReportSqlTracesSent(slowestTraces.Count);
            var responseStatus = DataTransportService.Send(slowestTraces);

            HandleResponse(responseStatus, slowestTraces);
        }

        private void HandleResponse(DataTransportResponseStatus responseStatus, ICollection<SqlTraceWireModel> traces)
        {
            switch (responseStatus)
            {
                case DataTransportResponseStatus.ServiceUnavailableError:
                case DataTransportResponseStatus.ConnectionError:
                    Retain(traces);
                    break;
                case DataTransportResponseStatus.PostTooBigError:
                case DataTransportResponseStatus.OtherError:
                case DataTransportResponseStatus.RequestSuccessful:
                default:
                    break;
            }
        }

        private void Retain(ICollection<SqlTraceWireModel> traces)
        {
            _agentHealthReporter.ReportSqlTracesRecollected(traces.Count);

            var tracesCollection = new SqlTraceStatsCollection();
            traces.ForEach(tracesCollection.Insert);

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
