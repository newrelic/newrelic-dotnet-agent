// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using NewRelic.Agent.Tests.TestSerializationHelpers.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    [JsonConverter(typeof(JsonArrayConverter))]
    public class UpdateLoadedModulesPayload
    {
        [JsonArrayIndex(Index = 0)] public const string Jars = "Jars";

        [JsonArrayIndex(Index = 1)] public readonly IList<UpdateLoadedModulesAssembly> Assemblies;

        public UpdateLoadedModulesPayload() : this(new List<UpdateLoadedModulesAssembly>())
        { }

        public UpdateLoadedModulesPayload(IList<UpdateLoadedModulesAssembly> assemblies)
        {
            Assemblies = assemblies;
        }
    }

    [JsonConverter(typeof(JsonArrayConverter))]
    public class UpdateLoadedModulesAssembly
    {
        [JsonArrayIndex(Index = 0)] public readonly string AssemblyName;

        [JsonArrayIndex(Index = 1)] public readonly string AssemblyVersion;

        [JsonArrayIndex(Index = 2)] public readonly IDictionary<string, string> Data;

        public UpdateLoadedModulesAssembly()
        {
            Data = new Dictionary<string, string>();
        }

        public UpdateLoadedModulesAssembly(string assemblyName, string assemblyVersion, IDictionary<string, string> data)
        {
            AssemblyName = assemblyName;
            AssemblyVersion = assemblyVersion;
            Data = data;
        }
    }
}
