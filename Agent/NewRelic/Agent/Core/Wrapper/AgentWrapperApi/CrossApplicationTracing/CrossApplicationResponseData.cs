using System;
using System.IO;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Utilities;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing
{
	// Note: this data is referred to as "AppData" in the CAT spec.
	public class CrossApplicationResponseData : IManualSerializable
	{
		// This object doesn't need an IsValid due to the contructors always producing a valid object.
		private const int TotalProperties = 7;
		private const int MinimumProperties = 5;
		private const int CrossProcessIdIndex = 0;
		private const int TransactionNameIndex = 1;
		private const int QueueTimeInSecondsIndex = 2;
		private const int ResponseTimeInSecondsIndex = 3;
		private const int ContentLengthIndex = 4;
		private const int TransactionGuidIndex = 5;
		private const int UnusedIndex = 6;

		public readonly string CrossProcessId;
		public readonly string TransactionName;
		public readonly float QueueTimeInSeconds;
		public readonly float ResponseTimeInSeconds;
		public readonly long ContentLength;
		public readonly string TransactionGuid; //optional
		public readonly bool Unused; //optional

		// For backwards compatibility we need to support deserializing AppData that is missing fields 5 and 6
		public CrossApplicationResponseData(string crossProcessId, string transactionName, float queueTimeInSeconds, float responseTimeInSeconds, long contentLength)
		{
			CrossProcessId = crossProcessId;
			TransactionName = transactionName;
			QueueTimeInSeconds = queueTimeInSeconds;
			ResponseTimeInSeconds = responseTimeInSeconds;
			ContentLength = contentLength;
		}

		// For backwards compatibility we need to support deserializing AppData that is missing field 6
		public CrossApplicationResponseData(string crossProcessId, string transactionName, float queueTimeInSeconds, float responseTimeInSeconds, long contentLength, string transactionGuid) : this(crossProcessId, transactionName, queueTimeInSeconds, responseTimeInSeconds, contentLength)
		{
			TransactionGuid = transactionGuid;
		}

		public CrossApplicationResponseData(string crossProcessId, string transactionName, float queueTimeInSeconds, float responseTimeInSeconds, long contentLength, string transactionGuid, bool unused) : this(crossProcessId, transactionName, queueTimeInSeconds, responseTimeInSeconds, contentLength, transactionGuid)
		{
			Unused = unused;
		}

		/// <summary>
		/// Deserialize a JSON string into a CrossApplicationResponseData.
		/// </summary>
		/// <param name="json">A JSON string representing a CrossApplicationResponseData</param>
		/// <returns>A CrossApplicationResponseData</returns>
		public static CrossApplicationResponseData TryBuildIncomingDataFromJson(string json)
		{
			if (string.IsNullOrEmpty(json))
			{
				return null;
			}

			try
			{
				var stringArray = JsonArrayHandlers.ConvertJsonToStringArrayForCat(json, MinimumProperties, TotalProperties);

				if (stringArray == null)
				{
					return null;
				}

				return new CrossApplicationResponseData(
					stringArray[CrossProcessIdIndex],
					stringArray[TransactionNameIndex],
					float.Parse(stringArray[QueueTimeInSecondsIndex]),
					float.Parse(stringArray[ResponseTimeInSecondsIndex]),
					long.Parse(stringArray[ContentLengthIndex]),
					stringArray[TransactionGuidIndex],
					bool.Parse(stringArray[UnusedIndex] ?? "false")
				);
			}
			catch (Exception)
			{
				return null;
			}
		}

		/// <summary>
		/// Serialize this to an JSON string.
		/// </summary>
		/// <returns>The serialized JSON string</returns>
		public string ToJson()
		{
			using (var stringWriter = new StringWriter())
			{
				using (var jsonWriter = new JsonTextWriter(stringWriter))
				{
					jsonWriter.WriteStartArray();
					jsonWriter.WriteValue(CrossProcessId);
					jsonWriter.WriteValue(TransactionName);
					jsonWriter.WriteValue(QueueTimeInSeconds);
					jsonWriter.WriteValue(ResponseTimeInSeconds);
					jsonWriter.WriteValue(ContentLength);
					jsonWriter.WriteValue(TransactionGuid);
					jsonWriter.WriteValue(Unused);
					jsonWriter.WriteEndArray();
				}

				return stringWriter.ToString();
			}
		}
	}
}
