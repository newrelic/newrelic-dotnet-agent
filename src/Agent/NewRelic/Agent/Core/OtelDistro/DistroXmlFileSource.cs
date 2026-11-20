// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Lists the top-level <c>*.xml</c> files of each directory that exists, in directory order and, within a directory,
/// ordered by file name ignoring case, with each file's last write time and length from the directory listing (no file is
/// opened). <see cref="Read"/> reads one file's bytes. A file that cannot be read is returned with null bytes and the
/// exception message as its read error; this class does not log (the caller reports unreadable files). Subfolders and
/// other extensions are ignored. An error enumerating a directory propagates to the caller. The first directory is the
/// root (the extensions folder); see <see cref="DirectoriesFor"/>.
/// </summary>
public sealed class DistroXmlFileSource : IDistroXmlFileSource
{
    private const string XmlExtension = ".xml";

    private readonly IReadOnlyList<string> _directories;

    /// <summary>The first directory; null when there are none or it is empty.</summary>
    public string RootDirectory { get; }

    public bool RootExists => RootDirectory != null && Directory.Exists(RootDirectory);

    public DistroXmlFileSource(IReadOnlyList<string> directories)
    {
        _directories = directories ?? throw new ArgumentNullException(nameof(directories));
        RootDirectory = directories.Count > 0 && !string.IsNullOrEmpty(directories[0]) ? directories[0] : null;
    }

    /// <summary>
    /// The directories the NR profiler reads instrumentation XML from in agent mode, for a given extensions directory: the
    /// directory itself, then its runtime subfolder (<c>netcore</c> on .NET, <c>netframework</c> on .NET Framework). Empty
    /// when there is no extensions directory. Whether they exist is checked when they are read or watched.
    /// </summary>
    public static IReadOnlyList<string> DirectoriesFor(string extensionsDirectory)
    {
        if (string.IsNullOrEmpty(extensionsDirectory))
        {
            return new string[0];
        }

        return new[] { extensionsDirectory, Path.Combine(extensionsDirectory, AgentInstallConfiguration.RuntimeDirectoryName) };
    }

    public IReadOnlyList<DistroXmlFileEntry> List()
    {
        var files = new List<DistroXmlFileEntry>();
        foreach (var directory in _directories)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                continue;
            }

            // All files, filtered here: a "*.xml" search pattern also matches longer extensions such as ".xmlx" on Windows.
            // FileInfo from a listing carries the stamp the enumeration returned, so no file is opened.
            var infos = new DirectoryInfo(directory).GetFiles();
            Array.Sort(infos, (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
            foreach (var info in infos)
            {
                if (string.Equals(info.Extension, XmlExtension, StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(new DistroXmlFileEntry(info.Name, info.FullName, info.LastWriteTimeUtc, info.Length));
                }
            }
        }

        return files;
    }

    public DistroXmlFile Read(DistroXmlFileEntry file)
    {
        try
        {
            return new DistroXmlFile(file.HomeName, file.Path, File.ReadAllBytes(file.Path), null);
        }
        catch (Exception ex)
        {
            return new DistroXmlFile(file?.HomeName, file?.Path, null, ex.Message);
        }
    }
}
