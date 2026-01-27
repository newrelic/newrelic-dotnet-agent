// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.WireModels;

public class LoadedModuleWireModel
{
    public string AssemblyName { get; }

    public string Version { get; }

    public Dictionary<string, object> Data { get; }

    public LoadedModuleWireModel(string assemblyName, string version)
    {
        AssemblyName = assemblyName;
        Version = version;
        Data = new Dictionary<string, object>();
    }
}