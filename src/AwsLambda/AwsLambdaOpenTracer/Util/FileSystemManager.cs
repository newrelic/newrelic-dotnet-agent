// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;

namespace NewRelic.OpenTracing.AmazonLambda
{
    internal interface IFileSystemManager
    {
        bool Exists(string path);

        void WriteAllText(string path, string contents);
    }

    internal class FileSystemManager : IFileSystemManager
    {
        public bool Exists(string path)
        {
            return File.Exists(path);
        }

        public void WriteAllText(string path, string contents)
        {
            File.WriteAllText(path, contents);
        }
    }
}
