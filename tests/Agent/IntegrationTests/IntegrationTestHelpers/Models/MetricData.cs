﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.IntegrationTestHelpers.Models
{
	[JsonConverter(typeof(MetricDataConverter))]
	public class MetricData
	{
		// index 0
		public readonly String Unknown1;

		// index 1
		public readonly Double Unknown2;

		// index 2
		public readonly Double Unknown3;

		// index 3
		public readonly IEnumerable<Metric> Metrics;

		public MetricData(String unknown1, Double unknown2, Double unknown3, IEnumerable<Metric> metrics)
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

				var unknown1 = (jArray[0] ?? new JObject()).ToObject<String>(serializer);
				var unknown2 = (jArray[1] ?? new JObject()).ToObject<Double>(serializer);
				var unknown3 = (jArray[2] ?? new JObject()).ToObject<Double>(serializer);
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

		public override String ToString()
		{
			return $"{MetricSpec.Name} ({MetricSpec.Scope}): {Values.CallCount}, {Values.Total}, {Values.TotalExclusive}, {Values.Min}, {Values.Max}, {Values.SumOfSquares}";
		}
	}

	public class MetricSpec
	{
		[JsonProperty(PropertyName = "name")]
		public readonly String Name;

		[JsonProperty(PropertyName = "scope")]
		public readonly String Scope;

		public MetricSpec(String name, String scope)
		{
			Name = name;
			Scope = scope;
		}
	}

	[JsonConverter(typeof(MetricValuesConverter))]
	public class MetricValues
	{
		// index 0
		public readonly UInt64 CallCount;

		// index 1
		public readonly Double Total;

		// index 2
		public readonly Double TotalExclusive;

		// index 3
		public readonly Double Min;

		// index 4
		public readonly Double Max;

		// index 5
		public readonly Double SumOfSquares;

		public MetricValues(UInt64 callCount, Double total, Double totalExclusive, Double min, Double max, Double sumOfSquares)
		{
			CallCount = callCount;
			Total = total;
			TotalExclusive = totalExclusive;
			Min = min;
			Max = max;
			SumOfSquares = sumOfSquares;
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

				var callCount = (jArray[0] ?? new JObject()).ToObject<UInt64>(serializer);
				var total = (jArray[1] ?? new JObject()).ToObject<Double>(serializer);
				var totalExclusive = (jArray[2] ?? new JObject()).ToObject<Double>(serializer);
				var min = (jArray[3] ?? new JObject()).ToObject<Double>(serializer);
				var max = (jArray[4] ?? new JObject()).ToObject<Double>(serializer);
				var sumOfSquares = (jArray[5] ?? new JObject()).ToObject<Double>(serializer);

				return new MetricValues(callCount, total, totalExclusive, min, max, sumOfSquares);
			}

			public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
			{
				throw new NotImplementedException();
			}
		}

	}
}
