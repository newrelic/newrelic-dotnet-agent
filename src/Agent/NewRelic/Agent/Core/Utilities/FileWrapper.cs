// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;

namespace NewRelic.Agent.Core.Utilities
{
    /// <summary>
    /// Wraps some File methods to allow for unit testing
    /// </summary>
    public interface IFileWrapper
    {
        bool Exists(string path);
        FileStream OpenWrite(string path);
    }

    public class FileWrapper : IFileWrapper
    {
        public bool Exists(string path)
        {
            return File.Exists(path);

        }

        public FileStream OpenWrite(string path)
        {
            return File.OpenWrite(path);
        }

    }
}
