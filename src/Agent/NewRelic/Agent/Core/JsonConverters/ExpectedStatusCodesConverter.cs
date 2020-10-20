// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Core.JsonConverters
{
    public class ExpectedStatusCodesConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer)
        {
            if (reader == null)
                return null;
            if (type == null)
                return null;
            if (serializer == null)
                return null;
            if (reader.TokenType == JsonToken.Null)
                return null;

            var jt = JToken.Load(reader);

            if (jt.Type == JTokenType.Array)
            {
                var array = jt.ToObject<string[]>();
                return new ExpectedStatusCodes(array);
            }

            return new ExpectedStatusCodes(new[] { jt.ToString() });
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

}
