// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Reflection;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// What <see cref="DistroOtelDerivedPrecedenceWatcher"/> needs to know, on .NET Framework, before it resolves a type: which
/// assemblies are loaded, and whether a probing bind for a reference would find a file. Answering must never bind.
/// </summary>
public interface IDistroDependencyProbe
{
    /// <summary>Simple names of the assemblies loaded in the AppDomain now.</summary>
    IEnumerable<string> GetLoadedAssemblyNames();

    /// <summary>True when a file exists where a probing bind for <paramref name="reference"/> would look.</summary>
    bool HasProbingFile(AssemblyName reference, Assembly referencingAssembly);
}
