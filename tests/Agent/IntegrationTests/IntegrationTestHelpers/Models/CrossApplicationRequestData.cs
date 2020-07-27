using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.IntegrationTestHelpers.Models
{
    // Note: this data is referred to as "TransactionData" in the CAT spec.
    [JsonConverter(typeof(CrossApplicationRequestDataConverter))]
    public class CrossApplicationRequestData
    {
        public readonly String TransactionGuid;
        public readonly Boolean Unused;
        public readonly String TripId;
        public readonly String PathHash;

        // For backwards compatibility we need to support deserializing transactionData that may be missing any number of fields
        public CrossApplicationRequestData()
        {

        }

        public CrossApplicationRequestData(String transactionGuid)
        {
            TransactionGuid = transactionGuid;
        }

        public CrossApplicationRequestData(String transactionGuid, Boolean unused)
        {
            TransactionGuid = transactionGuid;
            Unused = unused;
        }

        public CrossApplicationRequestData(String transactionGuid, Boolean unused, String tripId)
        {
            TransactionGuid = transactionGuid;
            Unused = unused;
            TripId = tripId;
        }

        public CrossApplicationRequestData(String transactionGuid, Boolean unused, String tripId, String pathHash)
        {
            TransactionGuid = transactionGuid;
            Unused = unused;
            TripId = tripId;
            PathHash = pathHash;
        }
        public class CrossApplicationRequestDataConverter : JsonConverter
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

                var transactionGuid = jArray[0].ToObject<String>();
                var unused = (jArray[1] ?? new JObject()).ToObject<Boolean>();
                var tripId = (jArray[2] ?? new JObject()).ToObject<String>();
                var pathHash = (jArray[3] ?? new JObject()).ToObject<String>();

                return new CrossApplicationRequestData(transactionGuid, unused, tripId, pathHash);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (writer == null)
                    throw new NullReferenceException(nameof(writer));
                if (value == null)
                    throw new NullReferenceException(nameof(value));

                var requestData = value as CrossApplicationRequestData;
                if (requestData == null)
                    throw new NullReferenceException(nameof(requestData));

                writer.WriteStartArray();
                writer.WriteValue(requestData.TransactionGuid);
                writer.WriteValue(requestData.Unused);
                writer.WriteValue(requestData.TripId);
                writer.WriteValue(requestData.PathHash);
                writer.WriteEndArray();
            }
        }
    }
}
