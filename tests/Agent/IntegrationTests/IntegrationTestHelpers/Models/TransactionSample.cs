using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.IntegrationTestHelpers.Models
{
	[JsonConverter(typeof(TransactionSampleConverter))]
	public class TransactionSample
	{
		// index 0
		public readonly DateTime Timestamp;

		// index 1
		public readonly TimeSpan Duration;

		// index 2
		public readonly String Path;

		// index 3
		public readonly String Uri;

		// index 4
		public readonly TransactionTrace TraceData;

		// index 5
		public readonly String Guid;

		// index 6
		public readonly Object Unused1 = null;

		// index 7
		public readonly Boolean ForcePersist;

		// index 8
		public readonly UInt64? XRaySessionId;

		public TransactionSample(DateTime timestamp, TimeSpan duration, String path, String uri, TransactionTrace traceData, String guid, Boolean forcePersist, UInt64? xRaySessionId)
		{
			Timestamp = timestamp;
			Duration = duration;
			Path = path;
			Uri = uri;
			TraceData = traceData;
			Guid = guid;
			ForcePersist = forcePersist;
			XRaySessionId = xRaySessionId;
		}

		public class TransactionSampleConverter : JsonConverter
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

				var timestamp = new DateTime(1970, 01, 01) + TimeSpan.FromSeconds((Double)(jArray[0] ?? 0));
				var duration = TimeSpan.FromMilliseconds((Double) (jArray[1] ?? 0));
				var path = (jArray[2] ?? new JObject()).ToObject<String>();
				var uri = (jArray[3] ?? new JObject()).ToObject<String>();
				var traceData = (jArray[4] ?? new JObject()).ToObject<TransactionTrace>();
				var guid = (jArray[5] ?? new JObject()).ToObject<String>();
				var forcePersist = (jArray[7] ?? new JObject()).ToObject<Boolean>();
				var xRaySessionId = (jArray[8] ?? new JObject()).ToObject<UInt64?>();

				return new TransactionSample(timestamp, duration, path, uri, traceData, guid, forcePersist, xRaySessionId);
			}

			public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
			{
				throw new NotImplementedException();
			}
		}
	}

}
