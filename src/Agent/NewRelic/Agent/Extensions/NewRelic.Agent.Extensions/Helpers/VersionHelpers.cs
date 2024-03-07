// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Extensions.Helpers
{
    public static class VersionHelpers
    {
        /// <summary>
        /// Gets the version of a library from the full name of the assembly.
        /// </summary>
        /// <param name="assemblyFullName"></param>
        /// <returns>Full version string for a library.</returns>
        public static string GetLibraryVersion(string assemblyFullName)
        {
            if (string.IsNullOrWhiteSpace(assemblyFullName))
            {
                return string.Empty;
            }

            var versionString = "Version=";
            var start = assemblyFullName.IndexOf(versionString, StringComparison.Ordinal) + versionString.Length;
            var length = assemblyFullName.IndexOf(',', start) - start;
            return assemblyFullName.Substring(start, length);
        }
    }
}
