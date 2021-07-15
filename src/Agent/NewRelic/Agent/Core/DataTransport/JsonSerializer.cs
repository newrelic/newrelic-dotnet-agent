// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.DataTransport
{
    public class JsonSerializer : ISerializer
    {
        private static readonly JsonConverter _eventAttributesJsonConverter = new EventAttributesJsonConverter();

        public string Serialize(object[] parameters)
        {
            return JsonConvert.SerializeObject(parameters, _eventAttributesJsonConverter);
        }

        public T Deserialize<T>(string responseBody)
        {
            return JsonConvert.DeserializeObject<T>(responseBody);
        }
    }
}
