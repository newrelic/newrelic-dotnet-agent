// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// The .NET Framework <see cref="IDistroDependencyProbe"/>: file and directory existence checks only, never a bind. An
/// approximation of a Fusion probing bind that errs toward "a file is there" (so toward checking now), not a faithful
/// replica: it looks in the referencing assembly's own directory (right for the LoadFrom context; also used for a
/// Load-context assembly), the app base and the privatePath entries relative to it (<c>name.dll</c>, <c>name.exe</c>,
/// <c>name\name.dll</c>, <c>name\name.exe</c>; a rooted entry is treated as not probed, which is conservative), the runtime
/// directory, and the GAC by simple name only (a <c>GAC*\name</c> directory, any version or key). No culture
/// subfolders, codeBase hints or binding redirects. It also ignores <c>AppDomainSetup.PrivateBinPathProbe</c>, which
/// ASP.NET sets to exclude the app base, so the probe can report a file where Fusion would not look; under IIS that
/// self-heals, because a bin change recycles the domain.
/// </summary>
public sealed class DistroDependencyProbe : IDistroDependencyProbe
{
    private static readonly string[] GacRoots =
    {
        Path.Combine("Microsoft.NET", "assembly", "GAC_MSIL"),
        Path.Combine("Microsoft.NET", "assembly", "GAC_64"),
        Path.Combine("Microsoft.NET", "assembly", "GAC_32"),
        Path.Combine("assembly", "GAC"),
        Path.Combine("assembly", "GAC_MSIL"),
        Path.Combine("assembly", "GAC_64"),
        Path.Combine("assembly", "GAC_32"),
    };

    private readonly List<string> _directories = new List<string>();
    private readonly List<string> _gacDirectories = new List<string>();

    public DistroDependencyProbe()
        : this(AppDomain.CurrentDomain.BaseDirectory, PrivateBinPath(), RuntimeEnvironment.GetRuntimeDirectory(), System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows))
    {
    }

    public DistroDependencyProbe(string applicationBase, string privateBinPath, string runtimeDirectory, string windowsDirectory)
    {
        if (!string.IsNullOrEmpty(applicationBase))
        {
            _directories.Add(applicationBase);
            foreach (var entry in (privateBinPath ?? string.Empty).Split(';'))
            {
                var trimmed = entry.Trim();
                if (trimmed.Length > 0 && !Path.IsPathRooted(trimmed))
                {
                    _directories.Add(Path.Combine(applicationBase, trimmed));
                }
            }
        }

        if (!string.IsNullOrEmpty(runtimeDirectory))
        {
            _directories.Add(runtimeDirectory);
        }

        if (!string.IsNullOrEmpty(windowsDirectory))
        {
            foreach (var root in GacRoots)
            {
                _gacDirectories.Add(Path.Combine(windowsDirectory, root));
            }
        }
    }

    /// <summary>
    /// The probe to gate the precedence check with on this runtime: one on .NET Framework, none (null) on .NET. Never
    /// throws.
    /// </summary>
    public static IDistroDependencyProbe ForCurrentRuntime() =>
        ForRuntime(() => RuntimeInformation.FrameworkDescription, () => new DistroDependencyProbe());

    /// <summary>
    /// <paramref name="createProbe"/>'s probe when <paramref name="describeRuntime"/> describes .NET Framework, null for
    /// .NET. Fails closed, never throws: when the runtime cannot be described, or the probe cannot be created, the result
    /// is a <see cref="DistroFailClosedDependencyProbe"/> (the gate stays on and every unloaded reference counts as
    /// missing), never null, which would resolve types with no gate and could cache a bind failure.
    /// </summary>
    public static IDistroDependencyProbe ForRuntime(Func<string> describeRuntime, Func<IDistroDependencyProbe> createProbe)
    {
        try
        {
            if (!IsNetFramework(describeRuntime()))
            {
                return null;
            }

            return createProbe() ?? new DistroFailClosedDependencyProbe();
        }
        catch (Exception)
        {
            return new DistroFailClosedDependencyProbe();
        }
    }

    /// <summary>True for a <c>RuntimeInformation.FrameworkDescription</c> of .NET Framework.</summary>
    public static bool IsNetFramework(string frameworkDescription) =>
        frameworkDescription != null && frameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase);

    public IEnumerable<string> GetLoadedAssemblyNames() => LoadedAssemblyNames();

    /// <summary>
    /// Simple names of the AppDomain's execution-context assemblies. <c>AppDomain.GetAssemblies</c> does not list
    /// reflection-only loads (those are <c>ReflectionOnlyGetAssemblies</c>), so none is counted as loaded.
    /// </summary>
    public static IEnumerable<string> LoadedAssemblyNames()
    {
        var names = new List<string>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            names.Add(assembly.GetName().Name);
        }

        return names;
    }

    public bool HasProbingFile(AssemblyName reference, Assembly referencingAssembly)
    {
        var name = reference?.Name;
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        var ownDirectory = DirectoryOf(referencingAssembly);
        if (ownDirectory != null && HasFile(ownDirectory, name))
        {
            return true;
        }

        foreach (var directory in _directories)
        {
            if (HasFile(directory, name))
            {
                return true;
            }
        }

        foreach (var gac in _gacDirectories)
        {
            if (Directory.Exists(Path.Combine(gac, name)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasFile(string directory, string name) =>
        File.Exists(Path.Combine(directory, name + ".dll"))
        || File.Exists(Path.Combine(directory, name + ".exe"))
        || File.Exists(Path.Combine(directory, name, name + ".dll"))
        || File.Exists(Path.Combine(directory, name, name + ".exe"));

    private static string DirectoryOf(Assembly assembly)
    {
        try
        {
            var location = assembly?.Location;
            return string.IsNullOrEmpty(location) ? null : Path.GetDirectoryName(location);
        }
        catch (Exception)
        {
            // A dynamic assembly has no location (NotSupportedException), nor has an Assembly subclass that does not
            // implement it. Then only the other directories are probed.
            return null;
        }
    }

    private static string PrivateBinPath()
    {
#if NETFRAMEWORK
        return AppDomain.CurrentDomain.SetupInformation.PrivateBinPath;
#else
        return null;
#endif
    }
}
