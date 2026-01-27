// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;

namespace NewRelic.Agent.Core.Utilities;

/// <summary>
/// Wraps some File methods to allow for unit testing
/// </summary>
public interface IFileWrapper
{
    bool Exists(string path);
    FileStream OpenWrite(string path);
    bool TryCreateFile(string path, bool deleteOnSuccess = true);
    string ReadAllText(string path);
    string[] ReadAllLines(string path);
    DateTime GetLastWriteTimeUtc(string path);
    FileStream Open(string path, FileMode mode, FileAccess access, FileShare share);
}

[NrExcludeFromCodeCoverage]
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

    public bool TryCreateFile(string path, bool deleteOnSuccess = true)
    {
        try
        {
            using var fs = File.Create(path, 1, deleteOnSuccess ? FileOptions.DeleteOnClose : FileOptions.None);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }

    public string[] ReadAllLines(string path)
    {
        return File.ReadAllLines(path);
    }

    public DateTime GetLastWriteTimeUtc(string path)
    {
        return File.GetLastWriteTimeUtc(path);
    }

    public FileStream Open(string path, FileMode mode, FileAccess access, FileShare share)
    {
        return File.Open(path, mode, access, share);
    }
}