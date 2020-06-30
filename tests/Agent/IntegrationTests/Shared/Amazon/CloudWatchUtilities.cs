/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.IntegrationTests.Shared.Amazon
{
    public static class CloudWatchUtilities
    {
        private const string NRLogIdentifier = "NR_LAMBDA_MONITORING";

        public static string DecodeAndDecompressNewRelicPayload(string source)
        {
            var byteArray = Convert.FromBase64String(source);

            using (GZipStream stream = new GZipStream(new MemoryStream(byteArray),
                       CompressionMode.Decompress))
            {
                const int size = 4096;
                byte[] buffer = new byte[size];
                using (MemoryStream memory = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    }
                    while (count > 0);
                    return Encoding.UTF8.GetString(memory.ToArray());
                }
            }
        }

        public static string GetSpanEventDataFromLog(List<string> logs)
        {
            var decoded = GetNewRelicPayloadFromLogs(logs);
            var spanEventData = JsonConvert.DeserializeObject<JObject>(decoded)["span_event_data"];
            return spanEventData[2].ToString();
        }

        public static string GetTransactionEventDataFromLog(List<string> logs)
        {
            var decoded = GetNewRelicPayloadFromLogs(logs);
            var transactionEventData = JsonConvert.DeserializeObject<JObject>(decoded)["analytic_event_data"];
            return transactionEventData[2].ToString();
        }

        public static string GetErrorEventDataFromLog(List<string> logs)
        {
            var decoded = GetNewRelicPayloadFromLogs(logs);
            var errorEventData = JsonConvert.DeserializeObject<JObject>(decoded)["error_event_data"];
            return errorEventData[2].ToString();
        }

        public static string GetErrorTraceDataFromLog(List<string> logs)
        {
            var decoded = GetNewRelicPayloadFromLogs(logs);
            var errorTraceData = JsonConvert.DeserializeObject<JObject>(decoded)["error_data"];
            return errorTraceData[1].ToString();
        }

        private static string GetNewRelicPayloadFromLogs(List<string> logs)
        {
            var nrLog = logs.Where(l => l.Contains(NRLogIdentifier)).LastOrDefault();
            var jsonPayload = JsonConvert.DeserializeObject<JArray>(nrLog);
            return CloudWatchUtilities.DecodeAndDecompressNewRelicPayload(jsonPayload[3].ToString());
        }
    }
}
