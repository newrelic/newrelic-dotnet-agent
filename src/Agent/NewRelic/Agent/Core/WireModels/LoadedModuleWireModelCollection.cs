// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels
{
    [JsonConverter(typeof(LoadedModuleWireModelCollectionJsonConverter))]
    public class LoadedModuleWireModelCollection
    {
        public List<LoadedModuleWireModel> LoadedModules { get; }

        public LoadedModuleWireModelCollection()
        {
            LoadedModules = new List<LoadedModuleWireModel>();
        }
    }
}
