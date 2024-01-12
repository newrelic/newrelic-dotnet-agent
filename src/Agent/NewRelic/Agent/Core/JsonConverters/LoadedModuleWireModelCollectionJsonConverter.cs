// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.WireModels;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.JsonConverters
{
    public class LoadedModuleWireModelCollectionJsonConverter : JsonConverter<LoadedModuleWireModelCollection>
    {
        // The payload is labeled "Jars" since the collector method was originally meant for and used by Java.
        private const string JarsName = "Jars";

        public override LoadedModuleWireModelCollection ReadJson(JsonReader reader, Type objectType, LoadedModuleWireModelCollection existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter jsonWriter, LoadedModuleWireModelCollection value, JsonSerializer serializer)
        {
            WriteJsonImpl(jsonWriter, value);
        }

        private static void WriteJsonImpl(JsonWriter jsonWriter, LoadedModuleWireModelCollection value)
        {
            jsonWriter.WriteValue(JarsName);

            jsonWriter.WriteStartArray();

            foreach (var loadedModule in value.LoadedModules)
            {
                // MODULE
                jsonWriter.WriteStartArray();

                jsonWriter.WriteValue(loadedModule.AssemblyName);
                jsonWriter.WriteValue(loadedModule.Version ?? " ");

                // DATA DICTIONARY
                jsonWriter.WriteStartObject();
                foreach (var item in loadedModule.Data)
                {
                    jsonWriter.WritePropertyName(item.Key);
                    jsonWriter.WriteValue(item.Value?.ToString() ?? " ");
                }

                jsonWriter.WriteEndObject();

                jsonWriter.WriteEndArray();
            }

            jsonWriter.WriteEndArray();
        }
    }
}
