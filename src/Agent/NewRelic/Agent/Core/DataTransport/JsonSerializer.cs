// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NewRelic.Agent.Core.DataTransport
{
    public class JsonSerializer : ISerializer
    {
        public string Serialize(object[] parameters)
        {
            var settings = new JsonSerializerSettings
            {
                Converters =
                [
                    new StringEnumConverter()  // honors [EnumMember(Value=...)]
                ],
                NullValueHandling = NullValueHandling.Ignore
            };
            return JsonConvert.SerializeObject(parameters, settings);
        }

        public T Deserialize<T>(string responseBody)
        {
            var settings = new JsonSerializerSettings
            {
                Converters =
                [
                    new StringEnumConverter()  // honors [EnumMember(Value=...)]
                ],
                NullValueHandling = NullValueHandling.Ignore
            };

            return JsonConvert.DeserializeObject<T>(responseBody, settings);
        }
    }
}
