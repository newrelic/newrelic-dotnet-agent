// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    [JsonConverter(typeof(SqlTraceConverter))]
    public class SqlTrace
    {
        // index 0
        public readonly string TransactionName;

        // index 1
        public readonly string Uri;

        // index 2
        public readonly int SqlId;

        //index 3
        public readonly string Sql;

        // index 4
        public readonly string DatastoreMetricName;

        // index 5
        public readonly uint CallCount;

        // index 6
        public readonly TimeSpan TotalCallTime;

        // index 7
        public readonly TimeSpan MinCallTime;

        // index 8
        public readonly TimeSpan MaxCallTime;

        // index 9 (decompressed, for convenience)
        public readonly IDictionary<string, object> ParameterData;

        public SqlTrace(string transactionName, string uri, int sqlId, string sql, string datastoreMetricName, uint callCount, TimeSpan totalCallTime, TimeSpan minCallTime, TimeSpan maxCallTime, IDictionary<string, object> parameterData)
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
            public override bool CanConvert(Type objectType)
            {
                return true;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var jArray = JArray.Load(reader);
                if (jArray == null)
                    throw new JsonSerializationException("Unable to create a jObject from reader.");

                var transactionName = (jArray[0] ?? new JObject()).ToObject<string>();
                var uri = (jArray[1] ?? new JObject()).ToObject<string>();
                var sqlId = (jArray[2] ?? new JObject()).ToObject<int>();
                var sql = (jArray[3] ?? new JObject()).ToObject<string>();
                var datastoreMetricName = (jArray[4] ?? new JObject()).ToObject<string>();
                var callCount = (jArray[5] ?? new JObject()).ToObject<uint>();
                var totalCallTime = TimeSpan.FromSeconds((double)(jArray[6] ?? 0));
                var minCallTime = TimeSpan.FromSeconds((double)(jArray[7] ?? 0));
                var maxCallTime = TimeSpan.FromSeconds((double)(jArray[8] ?? 0));

                var parameterData = (jArray[9] ?? new JObject()).ToObject<Dictionary<string, object>>();

                if (parameterData.ContainsKey("query_parameters"))
                {
                    var value = parameterData["query_parameters"] as JObject;
                    parameterData["query_parameters"] = value.ToObject<Dictionary<string, object>>();
                }

                return new SqlTrace(transactionName, uri, sqlId, sql, datastoreMetricName, callCount, totalCallTime, minCallTime, maxCallTime, parameterData);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }
}
