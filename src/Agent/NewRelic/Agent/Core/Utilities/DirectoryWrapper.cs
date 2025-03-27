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
        string[] GetFiles(string readOnlyPath, string pattern, SearchOption searchOption = SearchOption.TopDirectoryOnly);
        string GetCurrentDirectory();
        DirectoryInfo CreateDirectory(string path);
    }

    [NrExcludeFromCodeCoverage]
    public class DirectoryWrapper : IDirectoryWrapper
    {
        public static IDirectoryWrapper Instance { get; } = new DirectoryWrapper();

        public bool Exists(string path) => Directory.Exists(path);

        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly) => Directory.GetFiles(path, searchPattern, searchOption);

        public string GetCurrentDirectory() => Directory.GetCurrentDirectory();
        public DirectoryInfo CreateDirectory(string path) => Directory.CreateDirectory(path);
    }
}
