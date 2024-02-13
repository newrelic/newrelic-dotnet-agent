// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Commands;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Core.Logging;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.DataTransport
{
    /// <summary>
    /// Extends IDataTransportService with methods specific to serverless mode
    /// </summary>
    public interface IServerlessModeDataTransportService : IDataTransportService
    {
        /// <summary>
        /// Formats, compresses and writes the data collected from the various Send() methods 
        /// </summary>
        /// <returns></returns>
        bool FlushData();

        // TODO: add a method for populating this service with the required metadata (probably already in wire model format?)
        void SetMetadata(string arn, string functionVersion, string executionEnvironment);
    }

    /// <summary>
    /// An IDataTransportService implementation specifically for use in serverless mode
    /// </summary>
    public class ServerlessModeDataTransportService : ConfigurationBasedService, IServerlessModeDataTransportService
    {

        public ServerlessModeDataTransportService()
        {
            _subscriptions.Add<FlushServerlessDataEvent>(OnFlushServerlessDataEvent);
        }

        private void OnFlushServerlessDataEvent(FlushServerlessDataEvent flushServerlessDataEvent)
        {
            FlushData();
        }


        private ConcurrentDictionary<WireModelEventType, IEnumerable<IWireModel>> _events = new();

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // TODO: Anything relevant needed here?
        }

        public DataTransportResponseStatus Send(IEnumerable<TransactionTraceWireModel> transactionSampleDatas)
        {
            _events.AddOrUpdate(WireModelEventType.TransactionTraces, _ => transactionSampleDatas, (_, models) => models.Concat(transactionSampleDatas));

            return DataTransportResponseStatus.RequestSuccessful;
        }

        public DataTransportResponseStatus Send(IEnumerable<ErrorTraceWireModel> errorTraceDatas)
        {
            _events.AddOrUpdate(WireModelEventType.Errors, _ => errorTraceDatas, (_, models) => models.Concat(errorTraceDatas));

            return DataTransportResponseStatus.RequestSuccessful;
        }

        public DataTransportResponseStatus Send(IEnumerable<MetricWireModel> metrics)
        {
            _events.AddOrUpdate(WireModelEventType.Metrics, _ => metrics, (_, models) => models.Concat(metrics));

            return DataTransportResponseStatus.RequestSuccessful;
        }

        public DataTransportResponseStatus Send(EventHarvestData eventHarvestData, IEnumerable<TransactionEventWireModel> transactionEvents)
        {
            // TODO: What about eventHarvestData ??
            _events.AddOrUpdate(WireModelEventType.TransactionEvents, _ => transactionEvents, (_, models) => models.Concat(transactionEvents));

            return DataTransportResponseStatus.RequestSuccessful;
        }

        public DataTransportResponseStatus Send(EventHarvestData eventHarvestData, IEnumerable<ErrorEventWireModel> errorEvents)
        {
            // TODO: What about eventHarvestData ??
            _events.AddOrUpdate(WireModelEventType.ErrorEvents, _ => errorEvents, (_, models) => models.Concat(errorEvents));

            return DataTransportResponseStatus.RequestSuccessful;
        }

        public DataTransportResponseStatus Send(EventHarvestData eventHarvestData, IEnumerable<ISpanEventWireModel> enumerable)
        {
            // TODO: What about eventHarvestData ??
            _events.AddOrUpdate(WireModelEventType.SpanEvents, _ => enumerable, (_, models) => models.Concat(enumerable));

            return DataTransportResponseStatus.RequestSuccessful;
        }

        public DataTransportResponseStatus Send(IEnumerable<SqlTraceWireModel> sqlTraceWireModels)
        {
            _events.AddOrUpdate(WireModelEventType.SqlTraces, type => sqlTraceWireModels, (type, models) => models.Concat(sqlTraceWireModels));

            return DataTransportResponseStatus.RequestSuccessful;
        }

        public DataTransportResponseStatus Send(IEnumerable<CustomEventWireModel> customEvents)
        {
            _events.TryAdd(WireModelEventType.CustomEvents, customEvents);

            return DataTransportResponseStatus.RequestSuccessful;
        }

        #region IDataTransportService NotImplemented Methods
        public IEnumerable<CommandModel> GetAgentCommands() => throw new NotImplementedException();
        public void SendCommandResults(IDictionary<string, object> commandResults) => throw new NotImplementedException();
        public void SendThreadProfilingData(IEnumerable<ThreadProfilingModel> threadProfilingData) => throw new NotImplementedException();

        // TODO: Are log events supported for Lambda? Spec isn't clear. For now, just do nothing
        public DataTransportResponseStatus Send(LogEventWireModelCollection loggingEvents)
        {
            return DataTransportResponseStatus.RequestSuccessful;
        }

        // TODO: Is the module list supported for Lambda? Spec isn't clear.
        public DataTransportResponseStatus Send(LoadedModuleWireModelCollection loadedModules) => throw new NotImplementedException();
        #endregion

        public bool FlushData()
        {
            Log.Debug("ServerlessModeDataTransportService: FlushData starting.");
            // swap _events for a new, empty dictionary
            var eventsToFlush = Interlocked.Exchange(ref _events,
                new ConcurrentDictionary<WireModelEventType, IEnumerable<IWireModel>>());

            if (!eventsToFlush.Any())
            {
                Log.Debug("ServerlessModeDataTransportService: FlushData finished, no events to flush.");
                return false;
            }

            // Build a payload as per the Lambda spec, compressing portions of the data as per the spec
            var jsonPayload = BuildAndCompressPayload(eventsToFlush);

            // Write the payload to the /tmp/newrelic-telemetry file if it exists or to stdout if that file does not exist, as per the spec
            WritePayload(jsonPayload);

            Log.Debug("ServerlessModeDataTransportService: FlushData finished.");
            return true;
        }

        // TODO: this needs to be called somewhere early during execution of the lambda. Not sure what functionVersion is really for; see the spec
        public void SetMetadata(string arn, string functionVersion, string executionEnvironment)
        {
            throw new NotImplementedException();
        }

        private void WritePayload(string payloadJson)
        {
            bool success = false;
            var fileName = Path.Combine("/", "tmp", "newrelic-telemetry");
            try
            {
                var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

                if (File.Exists(fileName))
                {
                    using (var fs = File.OpenWrite(fileName))
                    {
                        fs.Write(payloadBytes, 0, payloadBytes.Length);
                        fs.Flush(true);
                    }

                    Log.Debug("Serverless payload written to {fileName}", fileName);
                    success = true;
                }
                else
                {
                    Log.Debug("Couldn't find {fileName} for writing serverless payload", fileName);
                }
            }
            catch (Exception e)
            {
                Log.Warn(e, "Failed to write serverless payload to {fileName}.", fileName);
            }

            if (!success)
            {
                // fall back to writing to stdout
                Log.Debug("Writing serverless payload to stdout");

                // TODO: Is this correct ?? 
                Console.WriteLine(payloadJson);
            }
        }

        private string BuildAndCompressPayload(
            ConcurrentDictionary<WireModelEventType, IEnumerable<IWireModel>> eventsToFlush)
        {

            var metadata = GetMetadata("foo", "bar");

            var compressiblePayload = GetCompressiblePayload(eventsToFlush);
            var compressedAndEncodedPayload = CompressAndEncode(JsonConvert.SerializeObject(compressiblePayload));

            var payload = new List<object> { 2, "NR_LAMBDA_MONITORING", metadata, compressedAndEncodedPayload };

            return JsonConvert.SerializeObject(payload);
        }

        private Dictionary<string, object> GetCompressiblePayload(
            ConcurrentDictionary<WireModelEventType, IEnumerable<IWireModel>> eventsToFlush)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            foreach (var kvp in eventsToFlush)
            {
                if (kvp.Value.Any())
                {
                    result[kvp.Key.ToJsonTag()] = kvp.Value;
                }
            }
            return result;
        }

        // gzip compress and base64 encode.
        private string CompressAndEncode(string compressiblePayload)
        {
            // TODO: no compression for testing, but note that file output will be an escaped json object
            return compressiblePayload;

            // TODO: Use this block when you want to do compression
            //try
            //{
            //    MemoryStream output = new MemoryStream();
            //    GZipStream gzip = new GZipStream(output, CompressionLevel.Optimal);
            //    var data = Encoding.UTF8.GetBytes(compressiblePayload);
            //    gzip.Write(data, 0, data.Length);
            //    gzip.Flush();
            //    gzip.Close();
            //    return Convert.ToBase64String(output.ToArray());
            //}
            //catch (IOException)
            //{
            //}
            //return string.Empty;
        }

        // Metadata is not compressed or encoded.
        private static IDictionary<string, object> GetMetadata(string arn, string executionEnv)
        {
            Dictionary<string, object> metadata = new Dictionary<string, object>
            {
                { "protocol_version", 16 }, // TODO: Is this the correct protocol version?
                { "arn", arn },
                { "execution_environment", executionEnv },
                { "agent_version", AgentInstallConfiguration.AgentVersion },
                { "metadata_version", 2 },
                { "agent_language", ".net core" } // TODO: Is this correct?
            };
            return metadata;
        }

    }

    internal enum WireModelEventType
    {
        TransactionTraces, // transaction_sample_data
        Metrics, // metric_data
        ErrorEvents, // error_event_data
        Errors, // error_data
        SpanEvents, // span_event_data
        SqlTraces, // sql_trace_data
        CustomEvents, // custom_event_data
        TransactionEvents // analytic_event_data
    }

    internal static class WireEventModelTypeHelpers
    {
        public static string ToJsonTag(this WireModelEventType wireModelEventType)
        {
            switch (wireModelEventType)
            {
                case WireModelEventType.TransactionTraces:
                    return "transaction_sample_data";
                case WireModelEventType.Metrics:
                    return "metric_data";
                case WireModelEventType.ErrorEvents:
                    return "error_event_data";
                case WireModelEventType.Errors:
                    return "error_data";
                case WireModelEventType.SpanEvents:
                    return "span_event_data";
                case WireModelEventType.SqlTraces:
                    return "sql_trace_data";
                case WireModelEventType.CustomEvents:
                    return "custom_event_data";
                case WireModelEventType.TransactionEvents:
                    return "analytic_event_data";
                default:
                    throw new ArgumentOutOfRangeException(nameof(wireModelEventType), wireModelEventType, null);
            }
        }
    }
}
