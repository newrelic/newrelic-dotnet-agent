// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Reflection;

namespace NewRelic.Agent.Core.OpenTelemetryBridge.Common
{
    /// <summary>
    /// Implementation of IAssemblyProvider that returns assemblies from the current AppDomain.
    /// </summary>
    public class AppDomainAssemblyProvider : IAssemblyProvider
    {
        public Assembly[] GetAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies();
        }
    }
}
