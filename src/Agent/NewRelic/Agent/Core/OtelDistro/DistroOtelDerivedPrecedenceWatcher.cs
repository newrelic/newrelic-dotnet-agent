// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Reflection;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Runs the Derived/Interface precedence check (<see cref="DistroOtelPrecedenceDiagnostics.ReportDerived"/>) when an
/// assembly that NR sites target is loaded: the NR target type is looked up by name and its base types and interfaces
/// are compared with the OTel Derived/Interface sites. Each loaded type is checked once.
/// <para>
/// Keyed on assembly load rather than on NR's first method resolution: a method OTel takes never runs NR's
/// integration, so it never reaches NR's resolver. Nothing here runs on an instrumented call.
/// </para>
/// <para>
/// On .NET Framework (a non-null <see cref="IDistroDependencyProbe"/>) a type is resolved only once every DIRECT
/// reference of its assembly is loaded (execution context; a reflection-only load does not count) or has a file where
/// the probe says a bind would find it. Resolving it earlier would bind a missing dependency, and the AppDomain caches
/// that bind failure: the application's own load fails later even after the file appears. Such an assembly is
/// deferred and checked on a later assembly load that is neither the assembly nor one of its references (inside those
/// loads the application may be loading the NR type itself, and resolving it then fails as a recursive load). While
/// deferred, a loaded reference is noticed on every load, but files are probed only on a backed-off schedule
/// (<see cref="MaxDeferredProbeInterval"/>). <see cref="OnAssemblyLoaded"/> must see every load, as the plugin's
/// AssemblyLoad handler does.
/// </para>
/// <para>
/// Scope, not a guarantee: only the direct references are gated, so an assembly missing further down a reference's
/// base chain (B present, but B's base type in a missing C) can still be bound early and cached as failed; and the
/// probe approximates Fusion and errs toward "a file is there" (see <see cref="DistroDependencyProbe"/>). Loaded
/// references are matched by simple name.
/// </para>
/// Never throws.
/// </summary>
public sealed class DistroOtelDerivedPrecedenceWatcher
{
    /// <summary>How many times a deferred site whose type fails to resolve is tried before it is given up.</summary>
    public const int MaxDeferredCheckAttempts = 3;

    /// <summary>
    /// While an assembly is deferred, its missing references' files are probed on its 1st, 2nd, 4th, ... eligible load
    /// (doubling), then every this many loads: bounded cost, and a file that appears later is still found.
    /// </summary>
    public const int MaxDeferredProbeInterval = 64;

    private readonly object _gate = new object();
    private readonly List<OtelSite> _derivedSites = new List<OtelSite>();
    private readonly Dictionary<string, List<NrSite>> _nrSitesByAssembly = new Dictionary<string, List<NrSite>>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Type> _checkedTypes = new HashSet<Type>();
    private readonly IDistroDependencyProbe _probe;
    private readonly HashSet<string> _loadedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<DeferredAssembly> _deferred = new List<DeferredAssembly>();
    private bool _loadedNamesSeeded;

    /// <summary>True when there is at least one OTel Derived/Interface site and one NR site to compare.</summary>
    public bool HasWork { get; }

    /// <summary>Gates on references on .NET Framework only (see <see cref="DistroDependencyProbe.ForCurrentRuntime"/>).</summary>
    public DistroOtelDerivedPrecedenceWatcher(IEnumerable<OtelSite> otelSites, IEnumerable<NrSite> nrSites)
        : this(otelSites, nrSites, DistroDependencyProbe.ForCurrentRuntime())
    {
    }

    /// <summary>With an explicit probe; a null <paramref name="probe"/> means no reference gate (the .NET behaviour).</summary>
    public DistroOtelDerivedPrecedenceWatcher(IEnumerable<OtelSite> otelSites, IEnumerable<NrSite> nrSites, IDistroDependencyProbe probe)
    {
        _probe = probe;
        try
        {
            foreach (var otel in otelSites ?? new OtelSite[0])
            {
                if (otel != null && otel.MatchesDerivedOrInterface)
                {
                    _derivedSites.Add(otel);
                }
            }

            foreach (var nr in nrSites ?? new NrSite[0])
            {
                if (nr == null || nr.Assembly == null)
                {
                    continue;
                }

                if (!_nrSitesByAssembly.TryGetValue(nr.Assembly, out var sites))
                {
                    sites = new List<NrSite>();
                    _nrSitesByAssembly.Add(nr.Assembly, sites);
                }

                sites.Add(nr);
            }
        }
        catch (Exception)
        {
            _derivedSites.Clear();
            _nrSitesByAssembly.Clear();
        }

        HasWork = _derivedSites.Count > 0 && _nrSitesByAssembly.Count > 0;
    }

    /// <summary>Checks the NR target types in <paramref name="assembly"/>, if NR sites target it. Never throws.</summary>
    public void OnAssemblyLoaded(Assembly assembly)
    {
        if (!HasWork || assembly == null || IsReflectionOnly(assembly))
        {
            return;
        }

        try
        {
            var name = assembly.GetName().Name;
            if (_probe != null)
            {
                lock (_gate)
                {
                    _loadedNames.Add(name);
                }

                RecheckDeferred(assembly, name);
            }

            if (!_nrSitesByAssembly.TryGetValue(name, out var sites))
            {
                return;
            }

            if (_probe != null && (IsDeferred(assembly) || !ReferencesBindable(assembly, sites)))
            {
                return;
            }

            foreach (var nr in sites)
            {
                CheckSite(assembly, nr, sites);
            }
        }
        catch (Exception)
        {
            // Diagnostics must never reach an assembly load.
        }
    }

    // A reflection-only load (AppDomain.AssemblyLoad fires for it on .NET Framework) neither makes a reference bindable
    // in the execution context nor can its types be checked. A subclass that does not implement the property is
    // treated as an execution-context assembly.
    private static bool IsReflectionOnly(Assembly assembly)
    {
        try
        {
            return assembly.ReflectionOnly;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // A repeated load event for a deferred assembly (the plugin's start-up pass and the event can both report it) is
    // left to the recheck on a later load.
    private bool IsDeferred(Assembly assembly)
    {
        lock (_gate)
        {
            foreach (var deferred in _deferred)
            {
                if (deferred.Assembly == assembly)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // True when every reference is loaded or has a probing file; otherwise defers the assembly (logged once) and returns
    // false. A probe or reference failure counts as missing: deferred, never resolved.
    private bool ReferencesBindable(Assembly assembly, List<NrSite> sites)
    {
        List<AssemblyName> missing;
        try
        {
            missing = MissingReferences(assembly, assembly.GetReferencedAssemblies(), true);
        }
        catch (Exception)
        {
            missing = null;
        }

        if (missing != null && missing.Count == 0)
        {
            return true;
        }

        var deferred = new DeferredAssembly(assembly, new List<NrSite>(sites), missing ?? new List<AssemblyName>(), ReferenceNames(assembly), missing == null);
        lock (_gate)
        {
            _deferred.Add(deferred);
        }

        LogDebug("OTel distro: deferring the Derived/Interface precedence check of {0}: {1} is neither loaded nor where a bind would look for it; checking again on a later assembly load.",
            assembly.GetName().Name, missing == null ? "a reference" : JoinNames(missing));
        return false;
    }

    // The references of `candidates` that are not loaded and, when `probeFiles`, have no probing file either. Throws if
    // the probe does.
    private List<AssemblyName> MissingReferences(Assembly assembly, IEnumerable<AssemblyName> candidates, bool probeFiles)
    {
        lock (_gate)
        {
            if (!_loadedNamesSeeded)
            {
                _loadedNames.UnionWith(_probe.GetLoadedAssemblyNames());
                _loadedNamesSeeded = true;
            }
        }

        var missing = new List<AssemblyName>();
        foreach (var reference in candidates)
        {
            if (reference?.Name == null)
            {
                continue;
            }

            bool loaded;
            lock (_gate)
            {
                loaded = _loadedNames.Contains(reference.Name);
            }

            if (!loaded && (!probeFiles || !_probe.HasProbingFile(reference, assembly)))
            {
                missing.Add(reference);
            }
        }

        return missing;
    }

    private void RecheckDeferred(Assembly loaded, string loadedName)
    {
        List<DeferredAssembly> due;
        lock (_gate)
        {
            if (_deferred.Count == 0)
            {
                return;
            }

            due = new List<DeferredAssembly>();
            foreach (var deferred in _deferred)
            {
                if (deferred.Assembly != loaded && !deferred.ReferenceNames.Contains(loadedName))
                {
                    due.Add(deferred);
                }
            }
        }

        foreach (var deferred in due)
        {
            Recheck(deferred);
        }
    }

    private void Recheck(DeferredAssembly deferred)
    {
        try
        {
            // Read and publish the entry's state under the lock, together: a concurrent recheck sees either the old
            // (Unknown, Missing) pair or the new one, never Unknown cleared with Missing not yet filled in.
            int loadEvent;
            bool unknown;
            List<AssemblyName> candidates;
            lock (_gate)
            {
                loadEvent = ++deferred.LoadEvents;
                unknown = deferred.Unknown;
                candidates = deferred.Missing;
            }

            var probeFiles = IsProbeDue(loadEvent);
            if (unknown && !probeFiles)
            {
                return;
            }

            // Every load notices a reference that is loaded now; files are probed only on the backed-off loads.
            var stillMissing = MissingReferences(deferred.Assembly, unknown ? deferred.Assembly.GetReferencedAssemblies() : candidates, probeFiles);
            lock (_gate)
            {
                deferred.Missing = stillMissing;
                deferred.Unknown = false;
            }

            if (stillMissing.Count > 0)
            {
                return;
            }
        }
        catch (Exception)
        {
            return;
        }

        lock (_gate)
        {
            // Another thread's load may have taken it already.
            if (!_deferred.Remove(deferred))
            {
                return;
            }
        }

        var name = deferred.Assembly.GetName().Name;
        LogDebug("OTel distro: running the deferred Derived/Interface precedence check of {0}.", name, null);
        var failed = new List<NrSite>();
        foreach (var nr in deferred.Sites)
        {
            if (!CheckSite(deferred.Assembly, nr, deferred.AllSites))
            {
                failed.Add(nr);
            }
        }

        if (failed.Count == 0)
        {
            return;
        }

        deferred.Attempts++;
        if (deferred.Attempts >= MaxDeferredCheckAttempts)
        {
            LogDebug("OTel distro: giving up the deferred Derived/Interface precedence check of {0} after {1} attempts.", name, MaxDeferredCheckAttempts);
            return;
        }

        deferred.Sites = failed;
        lock (_gate)
        {
            _deferred.Add(deferred);
        }
    }

    /// <summary>
    /// True on the 1-based eligible load events of a deferred assembly whose files are probed: 1, 2, 4, ... up to
    /// <see cref="MaxDeferredProbeInterval"/>, then every <see cref="MaxDeferredProbeInterval"/>th.
    /// </summary>
    public static bool IsProbeDue(int loadEvent) =>
        loadEvent <= MaxDeferredProbeInterval ? (loadEvent & (loadEvent - 1)) == 0 : loadEvent % MaxDeferredProbeInterval == 0;

    // One site at a time, so a type that fails to load (a missing dependency of its base type, say) does not stop the
    // other sites of the assembly. Failures are caught. False when resolving the type threw (worth a retry when the check
    // was deferred: a recursive load during the application's own load of the type throws, and is not cached).
    private bool CheckSite(Assembly assembly, NrSite nr, List<NrSite> sites)
    {
        Type type;
        try
        {
            type = assembly.GetType(nr.Type, false);
        }
        catch (Exception)
        {
            return false;
        }

        if (type == null)
        {
            return true;
        }

        lock (_gate)
        {
            if (!_checkedTypes.Add(type))
            {
                return true;
            }
        }

        DistroOtelPrecedenceDiagnostics.ReportDerived(type, _derivedSites, sites);
        return true;
    }

    private static HashSet<string> ReferenceNames(Assembly assembly)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (reference?.Name != null)
                {
                    names.Add(reference.Name);
                }
            }
        }
        catch (Exception)
        {
            // Unknown references: no load is skipped for this assembly but its own.
        }

        return names;
    }

    private static string JoinNames(List<AssemblyName> names)
    {
        var parts = new string[names.Count];
        for (var i = 0; i < names.Count; i++)
        {
            parts[i] = names[i].Name;
        }

        return string.Join(", ", parts);
    }

    private static void LogDebug(string format, object first, object second)
    {
        try
        {
            Log.Debug(format, first, second);
        }
        catch (Exception)
        {
        }
    }

    private sealed class DeferredAssembly
    {
        public DeferredAssembly(Assembly assembly, List<NrSite> sites, List<AssemblyName> missing, HashSet<string> referenceNames, bool unknown)
        {
            Assembly = assembly;
            AllSites = sites;
            Sites = sites;
            Missing = missing;
            ReferenceNames = referenceNames;
            Unknown = unknown;
        }

        public Assembly Assembly { get; }
        public List<NrSite> AllSites { get; }
        public HashSet<string> ReferenceNames { get; }
        public List<NrSite> Sites { get; set; }
        public List<AssemblyName> Missing { get; set; }

        // The references could not be read or probed at deferral: all of them are evaluated on the next recheck.
        public bool Unknown { get; set; }
        public int Attempts { get; set; }

        // Eligible load events seen while deferred, for the probe back-off.
        public int LoadEvents { get; set; }
    }
}
