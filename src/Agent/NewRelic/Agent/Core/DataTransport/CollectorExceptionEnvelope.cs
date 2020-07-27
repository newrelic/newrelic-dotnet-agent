using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using JetBrains.Annotations;
using NewRelic.SystemExtensions.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Core.DataTransport
{
    [JsonConverter(typeof(CollectorExceptionEnvelopeConverter))]
    public class CollectorExceptionEnvelope
    {
        [NotNull]
        public readonly Exception Exception;

        public CollectorExceptionEnvelope([NotNull] Exception exception)
        {
            Exception = exception;
        }
    }

    public class CollectorExceptionEnvelopeConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, Object value, Newtonsoft.Json.JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override Object ReadJson(JsonReader reader, Type objectType, Object existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            var jToken = JToken.Load(reader);
            if (jToken == null)
                throw new SerializationException();

            var exception = DeserializeExceptionFromCollectorResponse(jToken);
            return new CollectorExceptionEnvelope(exception);
        }

        [NotNull]
        private static Exception DeserializeExceptionFromCollectorResponse([NotNull] JToken jToken)
        {
            if (jToken.Type != JTokenType.Object)
                return new Exception(jToken.ToString());

            var dictionary = jToken.ToObject<IDictionary<String, Object>>();
            var message = dictionary.GetValueOrDefault("message") ?? jToken.ToString();

            var type = dictionary.GetValueOrDefault("error_type");
            if (type != null)
                return ExceptionFactories.NewException(type.ToString(), message.ToString());

            return new Exception(message.ToString());
        }

        public override Boolean CanConvert(Type objectType)
        {
            return objectType == typeof(CollectorExceptionEnvelope);
        }
    }
}
