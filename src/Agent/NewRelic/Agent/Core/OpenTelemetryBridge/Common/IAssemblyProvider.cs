// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace NewRelic.Agent.Core.OpenTelemetryBridge.Common;

/// <summary>
/// Provides access to loaded assemblies.
/// </summary>
public interface IAssemblyProvider
{
    /// <summary>
    /// Gets all assemblies currently loaded in the application domain.
    /// </summary>
    Assembly[] GetAssemblies();
}