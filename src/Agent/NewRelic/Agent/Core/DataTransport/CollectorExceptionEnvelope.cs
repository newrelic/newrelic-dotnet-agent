﻿/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using NewRelic.SystemExtensions.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Core.DataTransport
{
    [JsonConverter(typeof(CollectorExceptionEnvelopeConverter))]
    public class CollectorExceptionEnvelope
    {
        public readonly Exception Exception;

        public CollectorExceptionEnvelope(Exception exception)
        {
            Exception = exception;
        }
    }

    public class CollectorExceptionEnvelopeConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            var jToken = JToken.Load(reader);
            if (jToken == null)
                throw new SerializationException();

            var exception = DeserializeExceptionFromCollectorResponse(jToken);
            return new CollectorExceptionEnvelope(exception);
        }
        private static Exception DeserializeExceptionFromCollectorResponse(JToken jToken)
        {
            if (jToken.Type != JTokenType.Object)
                return new Exception(jToken.ToString());

            var dictionary = jToken.ToObject<IDictionary<string, object>>();
            var message = dictionary.GetValueOrDefault("message") ?? jToken.ToString();

            var type = dictionary.GetValueOrDefault("error_type");
            if (type != null)
                return ExceptionFactories.NewException(type.ToString(), message.ToString());

            return new Exception(message.ToString());
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(CollectorExceptionEnvelope);
        }
    }
}
