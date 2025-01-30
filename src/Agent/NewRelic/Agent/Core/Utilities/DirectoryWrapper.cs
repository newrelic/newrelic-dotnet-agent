// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;

namespace NewRelic.Agent.Core.Utilities
{
    /// <summary>
    /// Wraps some Directory methods to allow for unit testing
    /// </summary>
    public interface IDirectoryWrapper
    {
        bool Exists(string path);
        string[] GetFiles(string readOnlyPath, string yml);
    }

    [NrExcludeFromCodeCoverage]
    public class DirectoryWrapper : IDirectoryWrapper
    {
        public bool Exists(string path)
        {
            return Directory.Exists(path);
        }

        public string[] GetFiles(string path, string searchPattern)
        {
            return Directory.GetFiles(path, searchPattern);
        }
    }
}
