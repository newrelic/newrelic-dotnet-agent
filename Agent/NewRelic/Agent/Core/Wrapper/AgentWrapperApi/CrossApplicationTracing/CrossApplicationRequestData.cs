using System;
using System.IO;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Utilities;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing
{
	// Note: this data is referred to as "TransactionData" in the CAT spec.
	public class CrossApplicationRequestData : IManualSerializable
	{
		// The required amount for a valid object is 4, but this object should be returned with less.
		// Checking the validation is handle upstream in CatHeaderHandler.
		private const int TotalProperties = 4;
		private const int MinimumProperties = 0;

		private const int TransactionGuidIndex = 0;
		private const int UnusedIndex = 1;
		private const int TripIdIndex = 2;
		private const int PathHashIndex = 3;

		public readonly string TransactionGuid;
		public readonly bool Unused;
		public readonly string TripId;
		public readonly string PathHash;
		
		public CrossApplicationRequestData(string transactionGuid, bool unused, string tripId, string pathHash) 
		{
			TransactionGuid = transactionGuid;
			Unused = unused;
			TripId = tripId;
			PathHash = pathHash;
		}

		/// <summary>
		/// Deserialize a JSON string into a CrossApplicationRequestData.
		/// </summary>
		/// <param name="json">A JSON string representing a CrossApplicationRequestData</param>
		/// <returns>A CrossApplicationRequestData</returns>
		public static CrossApplicationRequestData TryBuildIncomingDataFromJson(string json)
		{
			if (string.IsNullOrEmpty(json))
			{
				return null;
			}

			try
			{
				var stringArray = JsonArrayHandlers.ConvertJsonToStringArrayForCat(json, MinimumProperties, TotalProperties);

				if(stringArray == null)
				{
					return null;
				}

				return new CrossApplicationRequestData(
					stringArray[TransactionGuidIndex],
					bool.Parse(stringArray[UnusedIndex]),
					stringArray[TripIdIndex],
					stringArray[PathHashIndex]
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
					jsonWriter.WriteValue(TransactionGuid);
					jsonWriter.WriteValue(Unused);
					jsonWriter.WriteValue(TripId);
					jsonWriter.WriteValue(PathHash);
					jsonWriter.WriteEndArray();
				}

				return stringWriter.ToString();
			}
		}
	}
}