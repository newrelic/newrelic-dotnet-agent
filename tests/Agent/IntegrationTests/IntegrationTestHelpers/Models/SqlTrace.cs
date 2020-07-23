using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.IntegrationTestHelpers.Models
{
    [JsonConverter(typeof(SqlTraceConverter))]
    public class SqlTrace
    {
        // index 0
        public readonly String TransactionName;

        // index 1
        public readonly String Uri;

        // index 2
        public readonly Int32 SqlId;

        //index 3
        public readonly String Sql;

        // index 4
        public readonly String DatastoreMetricName;

        // index 5
        public readonly UInt32 CallCount;

        // index 6
        public readonly TimeSpan TotalCallTime;

        // index 7
        public readonly TimeSpan MinCallTime;

        // index 8
        public readonly TimeSpan MaxCallTime;

        // index 9 (decompressed, for convenience)
        public readonly IDictionary<String, Object> ParameterData;

        public SqlTrace(String transactionName, String uri, Int32 sqlId, String sql, String datastoreMetricName, UInt32 callCount, TimeSpan totalCallTime, TimeSpan minCallTime, TimeSpan maxCallTime, IDictionary<String, Object> parameterData)
        {
            TransactionName = transactionName;
            Uri = uri;
            SqlId = sqlId;
            Sql = sql;
            DatastoreMetricName = datastoreMetricName;
            CallCount = callCount;
            TotalCallTime = totalCallTime;
            MinCallTime = minCallTime;
            MaxCallTime = maxCallTime;
            ParameterData = parameterData;
        }

        public class SqlTraceConverter : JsonConverter
        {
            public override Boolean CanConvert(Type objectType)
            {
                return true;
            }

            public override Object ReadJson(JsonReader reader, Type objectType, Object existingValue, JsonSerializer serializer)
            {
                var jArray = JArray.Load(reader);
                if (jArray == null)
                    throw new JsonSerializationException("Unable to create a jObject from reader.");

                var transactionName = (jArray[0] ?? new JObject()).ToObject<String>();
                var uri = (jArray[1] ?? new JObject()).ToObject<String>();
                var sqlId = (jArray[2] ?? new JObject()).ToObject<Int32>();
                var sql = (jArray[3] ?? new JObject()).ToObject<String>();
                var datastoreMetricName = (jArray[4] ?? new JObject()).ToObject<String>();
                var callCount = (jArray[5] ?? new JObject()).ToObject<UInt32>();
                var totalCallTime = TimeSpan.FromSeconds((Double)(jArray[6] ?? 0));
                var minCallTime = TimeSpan.FromSeconds((Double)(jArray[7] ?? 0));
                var maxCallTime = TimeSpan.FromSeconds((Double)(jArray[8] ?? 0));

                var parameterData = (jArray[9] ?? new JObject()).ToObject<Dictionary<String, Object>>();

                return new SqlTrace(transactionName, uri, sqlId, sql, datastoreMetricName, callCount, totalCallTime, minCallTime, maxCallTime, parameterData);
            }

            public override void WriteJson(JsonWriter writer, Object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }
}
