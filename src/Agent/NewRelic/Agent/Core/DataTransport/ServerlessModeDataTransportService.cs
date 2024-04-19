// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Commands;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Core;
using NewRelic.Core.Logging;
using NewRelic.SystemInterfaces;

namespace NewRelic.Agent.Core.DataTransport
{
    // These are just to make the long types more readable
    public class WireData : ConcurrentDictionary<string, object[]>;
    public class TransactionWireData : ConcurrentDictionary<string, WireData>;

    /// <summary>
    /// Extends IDataTransportService with methods specific to serverless mode
    /// </summary>
    public interface IServerlessModeDataTransportService : IDataTransportService
    {
        /// <summary>
        /// Formats, compresses and writes the data collected from the various Send() methods 
        /// </summary>
        /// <returns></returns>
        bool FlushData(string transactionId);
    }

    /// <summary>
    /// An IDataTransportService implementation specifically for use in serverless mode
    /// </summary>
    public class ServerlessModeDataTransportService : ConfigurationBasedService, IServerlessModeDataTransportService
    {
        private TransactionWireData _transactionWireData;
        private readonly IDateTimeStatic _dateTimeStatic;
        private readonly IServerlessModePayloadManager _serverlessModePayloadManager;
        private DateTime _lastMetricSendTime;
        private string _outputPath = $"{Path.DirectorySeparatorChar}tmp{Path.DirectorySeparatorChar}newrelic-telemetry";

        public ServerlessModeDataTransportService(IDateTimeStatic dateTimeStatic, IServerlessModePayloadManager serverlessModePayloadManager)
        {
            _dateTimeStatic = dateTimeStatic;
            _serverlessModePayloadManager = serverlessModePayloadManager;
            _lastMetricSendTime = _dateTimeStatic.UtcNow;
            _transactionWireData = new TransactionWireData();
            _subscriptions.Add<FlushServerlessDataEvent>(OnFlushServerlessDataEvent);
        }

        private void OnFlushServerlessDataEvent(FlushServerlessDataEvent flushServerlessDataEvent)
        {
            FlushData(flushServerlessDataEvent.TransactionId);
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
        }

        private void Enqueue(string transactionId, string type, params object[] data)
        {
            // We may not have a transaction Id if we're shutting down
            if (string.IsNullOrEmpty(transactionId))
            {
                return;
            }
            if (_transactionWireData.TryGetValue(transactionId, out WireData transactionData))
            {
                transactionData[type] = data;
            }
            else
            {
                WireData newData = new WireData();
                newData[type] = data;
                _transactionWireData[transactionId] = newData;
            }
        }

        public DataTransportResponseStatus Send(IEnumerable<TransactionTraceWireModel> transactionSampleDatas, string transactionId)
        {
            Enqueue(transactionId, "transaction_sample_data", _configuration.AgentRunId, transactionSampleDatas);
            return DataTransportResponseStatus.RequestSuccessful;
        }

        public DataTransportResponseStatus Send(IEnumerable<ErrorTraceWireModel> errorTraceDatas, string transactionId)
        {
            Enqueue(transactionId, "error_data", _configuration.AgentRunId, errorTraceDatas);
            return DataTransportResponseStatus.RequestSuccessful;
        }

        public DataTransportResponseStatus Send(IEnumerable<MetricWireModel> metrics, string transactionId)
        {
            if (!metrics.Any())
            {
                return DataTransportResponseStatus.RequestSuccessful;
            }

            var beginTime = _lastMetricSendTime;
            var endTime = _dateTimeStatic.UtcNow;

            var model = new MetricWireModelCollection(_configuration.AgentRunId as string, beginTime.ToUnixTimeSeconds(), endTime.ToUnixTimeSeconds(), metrics);

            Enqueue(transactionId, "metric_data", model);

            _lastMetricSendTime = endTime;

            return DataTransportResponseStatus.RequestSuccessful;
        }

        public DataTransportResponseStatus Send(EventHarvestData eventHarvestData, IEnumerable<TransactionEventWireModel> transactionEvents, string transactionId)
        {
            Enqueue(transactionId, "analytic_event_data", _configuration.AgentRunId, eventHarvestData, transactionEvents);
            return DataTransportResponseStatus.RequestSuccessful;
        }

        public DataTransportResponseStatus Send(EventHarvestData eventHarvestData, IEnumerable<ErrorEventWireModel> errorEvents, string transactionId)
        {
            Enqueue(transactionId, "error_event_data", _configuration.AgentRunId, eventHarvestData, errorEvents);
            return DataTransportResponseStatus.RequestSuccessful;
        }

        public DataTransportResponseStatus Send(EventHarvestData eventHarvestData, IEnumerable<ISpanEventWireModel> spanEvents, string transactionId)
        {
            Enqueue(transactionId, "span_event_data", _configuration.AgentRunId, eventHarvestData, spanEvents);
            return DataTransportResponseStatus.RequestSuccessful;
        }

        public DataTransportResponseStatus Send(IEnumerable<SqlTraceWireModel> sqlTraceWireModels, string transactionId)
        {
            Enqueue(transactionId, "sql_trace_data", sqlTraceWireModels);
            return DataTransportResponseStatus.RequestSuccessful;
        }

        public DataTransportResponseStatus Send(IEnumerable<CustomEventWireModel> customEvents, string transactionId)
        {
            Enqueue(transactionId, "custom_event_data", _configuration.AgentRunId, customEvents);
            return DataTransportResponseStatus.RequestSuccessful;
        }

        #region IDataTransportService NotImplemented Methods
        public IEnumerable<CommandModel> GetAgentCommands() => null;
        public void SendCommandResults(IDictionary<string, object> commandResults) { }
        public void SendThreadProfilingData(IEnumerable<ThreadProfilingModel> threadProfilingData) => throw new NotImplementedException();

        public DataTransportResponseStatus Send(LogEventWireModelCollection loggingEvents, string transactionId)
        {
            // Not supported in serverless mode
            return DataTransportResponseStatus.RequestSuccessful;
        }

        // Not supported in serverless mode
        public DataTransportResponseStatus Send(LoadedModuleWireModelCollection loadedModules, string transactionId) => throw new NotImplementedException();
        #endregion

        public bool FlushData(string transactionId)
        {
            WireData data;

            if (string.IsNullOrEmpty(transactionId))
            {
                Log.Error("Transaction id missing");
                return false;
            }

            if (!_transactionWireData.TryGetValue(transactionId, out data))
            {
                Log.Error("Transaction id '{0}' does not exist", transactionId);
                return false;
            }

            Log.Debug("ServerlessModeDataTransportService: FlushData starting.");

            if (!data.Any())
            {
                Log.Debug("ServerlessModeDataTransportService: FlushData finished, no events to flush.");
                return false;
            }

            // Build a payload as per the Lambda spec, compressing portions of the data as per the spec
            var jsonPayload = _serverlessModePayloadManager.BuildPayload(data);

            // Write the payload to the /tmp/newrelic-telemetry file if it exists or to stdout if that file does not exist, as per the spec
            _serverlessModePayloadManager.WritePayload(jsonPayload, _outputPath);

            // Done with this transaction
            if (!_transactionWireData.TryRemove(transactionId, out _))
            {
                Log.Warn("Failed to remove transaction {0}", transactionId);
            }

            Log.Debug("ServerlessModeDataTransportService: FlushData finished.");
            return true;
        }

        public static void SetMetadata(string functionVersion, string arn)
        {
            ServerlessModePayloadManager.SetMetadata(functionVersion, arn);
        }

    }
}
