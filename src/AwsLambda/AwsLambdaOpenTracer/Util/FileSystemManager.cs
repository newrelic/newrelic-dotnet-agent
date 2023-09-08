// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using NewRelic.Core.Logging;

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
            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                using StreamWriter writer = new StreamWriter(stream);
                writer.Write(contents);
                writer.Flush();
            }
            catch (Exception e)
            {
                Log.Error(e, "WriteAllText() failed");
            }
        }
    }
}
