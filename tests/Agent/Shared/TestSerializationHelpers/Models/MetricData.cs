// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    [JsonConverter(typeof(MetricDataConverter))]
    public class MetricData
    {
        // index 0
        public readonly string Unknown1;

        // index 1
        public readonly double Unknown2;

        // index 2
        public readonly double Unknown3;

        // index 3
        public readonly IEnumerable<Metric> Metrics;

        public MetricData(string unknown1, double unknown2, double unknown3, IEnumerable<Metric> metrics)
        {
            Unknown1 = unknown1;
            Unknown2 = unknown2;
            Unknown3 = unknown3;
            Metrics = metrics;
        }

        public class MetricDataConverter : JsonConverter
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

                var unknown1 = (jArray[0] ?? new JObject()).ToObject<string>(serializer);
                var unknown2 = (jArray[1] ?? new JObject()).ToObject<double>(serializer);
                var unknown3 = (jArray[2] ?? new JObject()).ToObject<double>(serializer);
                var metrics = (jArray[3] ?? new JObject()).ToObject<IEnumerable<Metric>>(serializer);

                return new MetricData(unknown1, unknown2, unknown3, metrics);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }

    [JsonConverter(typeof(MetricConverter))]
    public class Metric
    {
        // index 0
        public readonly MetricSpec MetricSpec;

        // index 1
        public readonly MetricValues Values;

        public Metric(MetricSpec metricSpec, MetricValues values)
        {
            MetricSpec = metricSpec;
            Values = values;
        }

        public class MetricConverter : JsonConverter
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

                var metricSpec = (jArray[0] ?? new JObject()).ToObject<MetricSpec>(serializer);
                var values = (jArray[1] ?? new JObject()).ToObject<MetricValues>();

                return new Metric(metricSpec, values);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }

        public override string ToString()
        {
            return $"{MetricSpec.Name} ({MetricSpec.Scope}): {Values.CallCount}, {Values.Total}, {Values.TotalExclusive}, {Values.Min}, {Values.Max}, {Values.SumOfSquares}";
        }
    }

    public class MetricSpec
    {
        [JsonProperty(PropertyName = "name")]
        public readonly string Name;

        [JsonProperty(PropertyName = "scope")]
        public readonly string Scope;

        public MetricSpec(string name, string scope)
        {
            Name = name;
            Scope = scope;
        }
    }

    [JsonConverter(typeof(MetricValuesConverter))]
    public class MetricValues
    {
        // index 0
        public ulong CallCount { get; private set; }

        // index 1
        public double Total { get; private set; }

        // index 2
        public double TotalExclusive { get; private set; }

        // index 3
        public double Min { get; private set; }

        // index 4
        public double Max { get; private set; }

        // index 5
        public double SumOfSquares { get; private set; }

        public MetricValues(ulong callCount, double total, double totalExclusive, double min, double max, double sumOfSquares)
        {
            CallCount = callCount;
            Total = total;
            TotalExclusive = totalExclusive;
            Min = min;
            Max = max;
            SumOfSquares = sumOfSquares;
        }

        public void Consolidate(MetricValues newValues)
        {
            CallCount += newValues.CallCount;
            Total += newValues.Total;
            TotalExclusive += newValues.TotalExclusive;
            Min = newValues.Min < Min ? newValues.Min : Min;
            Max = newValues.Max > Max ? newValues.Max : Max;
            SumOfSquares += newValues.SumOfSquares;
        }

        public class MetricValuesConverter : JsonConverter
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

                var callCount = (jArray[0] ?? new JObject()).ToObject<ulong>(serializer);
                var total = (jArray[1] ?? new JObject()).ToObject<double>(serializer);
                var totalExclusive = (jArray[2] ?? new JObject()).ToObject<double>(serializer);
                var min = (jArray[3] ?? new JObject()).ToObject<double>(serializer);
                var max = (jArray[4] ?? new JObject()).ToObject<double>(serializer);
                var sumOfSquares = (jArray[5] ?? new JObject()).ToObject<double>(serializer);

                return new MetricValues(callCount, total, totalExclusive, min, max, sumOfSquares);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }

    }
}
