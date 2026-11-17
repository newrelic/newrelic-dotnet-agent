// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ArtifactBuilder;

/// <summary>
/// Creates zip archives and normalizes NuGet packages so the bytes depend only on the content:
/// fixed entry timestamps, a fixed entry order, and no random names.
/// </summary>
public static class DeterministicArchive
{
    private const string ContentTypesEntryName = "[Content_Types].xml";
    private const string RelationshipsEntryName = "_rels/.rels";
    private const string CorePropertiesDirectory = "package/services/metadata/core-properties/";
    private const string CorePropertiesExtension = ".psmdcp";

    public static void CreateFromDirectory(string sourceDirectory, string zipFilePath, DateTimeOffset timestamp)
    {
        var root = Path.GetFullPath(sourceDirectory);
        var zipTimestamp = DeterministicTimestamp.ToZipTimestamp(timestamp);

        var directories = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .Where(directory => !Directory.EnumerateFileSystemEntries(directory).Any())
            .Select(directory => (Name: ToEntryName(root, directory) + "/", Path: (string)null));

        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => (Name: ToEntryName(root, file), Path: file));

        var entries = directories.Concat(files).OrderBy(entry => entry.Name, StringComparer.Ordinal);

        using var zipStream = new FileStream(zipFilePath, FileMode.CreateNew, FileAccess.Write);
        using var archive = new System.IO.Compression.ZipArchive(zipStream, ZipArchiveMode.Create);

        foreach (var (name, path) in entries)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            entry.LastWriteTime = zipTimestamp;
            entry.ExternalAttributes = 0;

            if (path == null) continue;

            using var entryStream = entry.Open();
            using var fileStream = File.OpenRead(path);
            fileStream.CopyTo(entryStream);
        }
    }

    /// <summary>
    /// Rewrites a package created by nuget.exe in place. Package contents and metadata are unchanged
    /// except for the name of the random core properties part (and the relationship that points to it).
    /// </summary>
    public static void NormalizeNupkg(string nupkgFilePath, DateTimeOffset timestamp)
    {
        var zipTimestamp = DeterministicTimestamp.ToZipTimestamp(timestamp);
        var entries = ReadEntries(nupkgFilePath);

        var coreProperties = entries.Keys.SingleOrDefault(name =>
            name.StartsWith(CorePropertiesDirectory, StringComparison.Ordinal) && name.EndsWith(CorePropertiesExtension, StringComparison.Ordinal));

        if (coreProperties != null)
        {
            var newName = CorePropertiesDirectory + ToHex(SHA256.HashData(entries[coreProperties]), 16) + CorePropertiesExtension;
            entries[newName] = entries[coreProperties];
            entries.Remove(coreProperties);

            entries[RelationshipsEntryName] = RewriteRelationships(entries[RelationshipsEntryName], coreProperties, newName);
        }

        var ordered = entries
            .OrderBy(entry => entry.Key == ContentTypesEntryName ? 0 : 1)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal);

        var temporaryPath = nupkgFilePath + ".normalized";
        using (var zipStream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write))
        using (var archive = new System.IO.Compression.ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            foreach (var (name, content) in ordered)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                entry.LastWriteTime = zipTimestamp;
                entry.ExternalAttributes = 0;

                using var entryStream = entry.Open();
                entryStream.Write(content, 0, content.Length);
            }
        }

        File.Move(temporaryPath, nupkgFilePath, true);
    }

    private static Dictionary<string, byte[]> ReadEntries(string zipFilePath)
    {
        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        using var archive = ZipFile.OpenRead(zipFilePath);
        foreach (var entry in archive.Entries)
        {
            using var entryStream = entry.Open();
            using var memory = new MemoryStream();
            entryStream.CopyTo(memory);
            entries[entry.FullName] = memory.ToArray();
        }

        return entries;
    }

    private static byte[] RewriteRelationships(byte[] relationshipsXml, string oldPartName, string newPartName)
    {
        using var reader = new MemoryStream(relationshipsXml);
        var document = XDocument.Load(reader);

        var oldTarget = "/" + oldPartName;
        var newTarget = "/" + newPartName;

        foreach (var relationship in document.Descendants().Where(element => element.Name.LocalName == "Relationship"))
        {
            var target = relationship.Attribute("Target");
            if (target == null || target.Value != oldTarget) continue;

            target.Value = newTarget;
            relationship.SetAttributeValue("Id", "R" + ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(newTarget)), 8).ToUpperInvariant());
        }

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true
        };

        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, settings))
        {
            document.Save(writer);
        }

        return output.ToArray();
    }

    private static string ToEntryName(string root, string path)
    {
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }

    private static string ToHex(byte[] bytes, int length)
    {
        return Convert.ToHexString(bytes, 0, length).ToLowerInvariant();
    }
}
