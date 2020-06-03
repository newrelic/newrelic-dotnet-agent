using NewRelic.OpenTracing.AmazonLambda.Events;
using NewRelic.OpenTracing.AmazonLambda.Traces;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace NewRelic.OpenTracing.AmazonLambda.Util
{
    internal class ProtocolUtil
    {
        // Metadata is not compressed or encoded.
        public static IDictionary<string, object> GetMetadata(string arn, string executionEnv)
        {
            Dictionary<string, object> metadata = new Dictionary<string, object>();
            metadata.Add("protocol_version", 16);
            metadata.Add("arn", arn);
            metadata.Add("execution_environment", executionEnv);
            metadata.Add("agent_version", "1.0.0");
            metadata.Add("metadata_version", 2);
            metadata.Add("agent_language", ".net core");
            return metadata;
        }

        public static IDictionary<string, object> GetData(IList<Event> spans, TransactionEvent transactionEvent, IList<ErrorEvent> errorEvents, IList<ErrorTrace> errorTraces)
        {
            var data = new Dictionary<string, object>();

            if (spans.Count > 0)
            {
                AddEvents(spans, data, "span_event_data");
            }

            if (transactionEvent != null)
            {
                AddEvents(new List<Event> { transactionEvent }, data, "analytic_event_data");
            }

            if (errorEvents.Count > 0)
            {
                AddEvents(errorEvents, data, "error_event_data");
            }

            if (errorTraces.Count > 0)
            {
                AddTraces(errorTraces, data, "error_data");
            }

            return data;
        }

        private static void AddEvents<T>(IList<T> events, IDictionary<string, object> data, string eventKey, string agentRunId = null) where T : Event
        {
            var list = new List<object>
            {
                agentRunId
            };

            var eventInfo = new Dictionary<string, object>();
            eventInfo.Add("events_seen", events.Count);
            eventInfo.Add("reservoir_size", events.Count);
            list.Add(eventInfo);

            var eventArray = new List<object[]>();
            foreach (Event e in events)
            {
                eventArray.Add(new object[] { e.Intrinsics, e.UserAttributes, e.AgentAttributes });
            }
            list.Add(eventArray);
            data.Add(eventKey, list);
        }

        private static void AddTraces(IList<ErrorTrace> errorTraces, IDictionary<string, object> data, string eventKey, string agentRunId = null)
        {

            var errorList = new List<object>()
            {
                agentRunId
            };

            var errorTraceList = new List<object>();

            foreach (ErrorTrace e in errorTraces)
            {
                errorTraceList.Add(e.BuildErrorTraceObject());
            }

            errorList.Add(errorTraceList);

            data.Add(eventKey, errorList);
        }

        // gzip compress and base64 encode.
        public static string CompressAndEncode(string source)
        {
            try
            {
                MemoryStream output = new MemoryStream();
                GZipStream gzip = new GZipStream(output, CompressionLevel.Optimal);
                var data = Encoding.UTF8.GetBytes(source);
                gzip.Write(data, 0, data.Length);
                gzip.Flush();
                gzip.Close();
                return Convert.ToBase64String(output.ToArray());
            }
            catch (IOException)
            {
            }
            return string.Empty;
        }
    }
}
