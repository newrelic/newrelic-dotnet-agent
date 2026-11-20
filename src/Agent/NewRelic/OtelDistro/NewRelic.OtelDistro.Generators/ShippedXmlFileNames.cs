// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.OtelDistro.Generators;

/// <summary>
/// Maps the path of an instrumentation XML file the generator is given to the file's home name: its file name in the
/// agent home's extensions directory, which is what the run time sees on disk. The build copies each wrapper's
/// <c>Wrapper\&lt;Dir&gt;\Instrumentation.xml</c> to <c>NewRelic.Providers.Wrapper.&lt;Dir&gt;.Instrumentation.xml</c>
/// (build/build_functions.ps1) and every other shipped file (Misc) under its own name.
/// </summary>
public static class ShippedXmlFileNames
{
    private const string WrapperSourceFileName = "Instrumentation.xml";
    private const string WrapperFolderName = "Wrapper";
    private const string HomeNamePrefix = "NewRelic.Providers.Wrapper.";
    private const string HomeNameSuffix = ".Instrumentation.xml";
    private const string PluginInstrumentationFolder = "/NewRelic.OtelDistro.Plugin/Instrumentation/";

    /// <summary>
    /// The home name of <paramref name="path"/> (either slash): <c>...\Wrapper\&lt;Dir&gt;\Instrumentation.xml</c> gives
    /// <c>NewRelic.Providers.Wrapper.&lt;Dir&gt;.Instrumentation.xml</c>; any other file name is its own home name (Misc,
    /// and the home copies the default build feeds the plugin from). Returns false, with a null
    /// <paramref name="homeName"/>, for the plugin's own XML (its <c>Instrumentation</c> folder; never shipped), an
    /// <c>Instrumentation.xml</c> not directly in a wrapper folder, and a null or empty path or file name. Never throws.
    /// </summary>
    public static bool TryGetHomeName(string path, out string homeName)
    {
        homeName = null;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        // A leading slash so that a relative path starting with the plugin folder matches too.
        var normalized = "/" + path.Replace('\\', '/');
        if (normalized.IndexOf(PluginInstrumentationFolder, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        var segments = normalized.Split('/');
        var fileName = segments[segments.Length - 1];
        if (fileName.Length == 0)
        {
            return false;
        }

        if (!string.Equals(fileName, WrapperSourceFileName, StringComparison.OrdinalIgnoreCase))
        {
            homeName = fileName;
            return true;
        }

        if (segments.Length < 3
            || segments[segments.Length - 2].Length == 0
            || !string.Equals(segments[segments.Length - 3], WrapperFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        homeName = HomeNamePrefix + segments[segments.Length - 2] + HomeNameSuffix;
        return true;
    }
}
