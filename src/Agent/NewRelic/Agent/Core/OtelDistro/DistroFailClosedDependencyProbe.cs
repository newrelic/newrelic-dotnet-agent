// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Reflection;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// The probe used on .NET Framework when <see cref="DistroDependencyProbe"/> cannot be created: no reference has a file,
/// so an assembly is checked only once all its references are loaded. Keeps the gate on rather than resolving types
/// with none.
/// </summary>
public sealed class DistroFailClosedDependencyProbe : IDistroDependencyProbe
{
    public IEnumerable<string> GetLoadedAssemblyNames() => DistroDependencyProbe.LoadedAssemblyNames();

    public bool HasProbingFile(AssemblyName reference, Assembly referencingAssembly) => false;
}
