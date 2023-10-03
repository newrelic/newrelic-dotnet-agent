// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Reflection;

namespace NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers
{
    [Library]
    public static class AssemblyHelper
    {
        [LibraryMethod]
        public static void LoadAssemblyFromFile(string relativePath)
        {
            var startAssembly = new FileInfo(Assembly.GetEntryAssembly().Location);
            var startAssemblyFolder = startAssembly.Directory.FullName;

            var loadAssemblyPath = Path.Combine(startAssemblyFolder, relativePath);
            
            Assembly.LoadFile(loadAssemblyPath);
        }
    }
}
