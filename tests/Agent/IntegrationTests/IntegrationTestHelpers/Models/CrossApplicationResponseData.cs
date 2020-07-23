using System;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.IntegrationTestHelpers.Models
{
	// Note: this data is referred to as "AppData" in the CAT spec.
	[JsonConverter(typeof(CrossApplicationResponseDataConverter)), UsedImplicitly]
	public class CrossApplicationResponseData
	{
		public readonly String CrossProcessId;
		public readonly String TransactionName;
		public readonly Single QueueTimeInSeconds;
		public readonly Single ResponseTimeInSeconds;
		public readonly Int32 ContentLength;
		public readonly String TransactionGuid;
		public readonly Boolean Unused;

		// For backwards compatibility we need to support deserializing AppData that is missing fields 5 and 6
		public CrossApplicationResponseData([NotNull] String crossProcessId, [NotNull] String transactionName, Single queueTimeInSeconds, Single responseTimeInSeconds, Int32 contentLength)
		{
			CrossProcessId = crossProcessId;
			TransactionName = transactionName;
			QueueTimeInSeconds = queueTimeInSeconds;
			ResponseTimeInSeconds = responseTimeInSeconds;
			ContentLength = contentLength;
		}

		// For backwards compatibility we need to support deserializing AppData that is missing field 6
		public CrossApplicationResponseData([NotNull] String crossProcessId, [NotNull] String transactionName, Single queueTimeInSeconds, Single responseTimeInSeconds, Int32 contentLength, String transactionGuid)
		{
			CrossProcessId = crossProcessId;
			TransactionName = transactionName;
			QueueTimeInSeconds = queueTimeInSeconds;
			ResponseTimeInSeconds = responseTimeInSeconds;
			ContentLength = contentLength;
			TransactionGuid = transactionGuid;
		}

		public CrossApplicationResponseData([NotNull] String crossProcessId, [NotNull] String transactionName, Single queueTimeInSeconds, Single responseTimeInSeconds, Int32 contentLength, String transactionGuid, Boolean unused)
		{
			CrossProcessId = crossProcessId;
			TransactionName = transactionName;
			QueueTimeInSeconds = queueTimeInSeconds;
			ResponseTimeInSeconds = responseTimeInSeconds;
			ContentLength = contentLength;
			TransactionGuid = transactionGuid;
			Unused = unused;
		}
		
		public class CrossApplicationResponseDataConverter : JsonConverter
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
				
				var crossProcessId = jArray[0].ToObject<String>();
				var transactionname = jArray[1].ToObject<String>();
                var queueTimeInSeconds = jArray[2].ToObject<Single>();
				var responseTimeInSeconds = jArray[3].ToObject<Single>();
				var contentLength = jArray[4].ToObject<Int32>();
				var transactionGuid = (jArray[5] ?? new JObject()).ToObject<String>();
				var unused = (jArray[6] ?? new JObject()).ToObject<Boolean>();

				return new CrossApplicationResponseData(crossProcessId, transactionname, queueTimeInSeconds, responseTimeInSeconds, contentLength, transactionGuid, unused);
			}

			public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
			{
				throw new NotImplementedException();
			}
		}
	}
}
