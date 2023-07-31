// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace NewRelic.Agent.TestUtilities
{
    public static class AssemblyExtensions
    {
        public static string GetLocation(this Assembly assembly)
        {
#if NETFRAMEWORK
            return assembly.CodeBase;
#else
            return assembly.Location;
#endif            

        }
    }
}
