// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NewRelic.OtelDistro.Coordinator;

/// <summary>
/// Sends each distinct definition to OTel native at most once per process, however many callers
/// (AppDomains) ask. Cross-domain callers reach one instance through remoting.
/// </summary>
public sealed class RegistrationCoordinator : MarshalByRefObject
{
    private readonly object _lock = new object();
    private readonly HashSet<string> _sent = new HashSet<string>(StringComparer.Ordinal);
    private readonly INativeRegistrar _native;
    private readonly int _processId;
    private int _nextId = 1;

    /// <summary>Creates a coordinator over the real native registrar for the current process.</summary>
    public RegistrationCoordinator() : this(new OtelNativeRegistrar(), CurrentProcessId())
    {
#if NETFRAMEWORK
        // Created by CreateInstanceFrom in the default domain, this assembly is in the LoadFrom context there; the
        // [Serializable] arguments of cross-domain calls are deserialized by name, which needs this resolver to bind.
        CoordinatorHost.InstallAssemblyResolver(typeof(RegistrationCoordinator).Assembly);
#endif
    }

    /// <summary>Creates a coordinator over the given registrar.</summary>
    public RegistrationCoordinator(INativeRegistrar native, int processId)
    {
        _native = native;
        _processId = processId;
    }

    private static int CurrentProcessId()
    {
        using (var process = Process.GetCurrentProcess())
        {
            return process.Id;
        }
    }

    /// <summary>Identity (assembly full name) of the coordinator code, so callers can tell which copy answered.</summary>
    public string Version => typeof(RegistrationCoordinator).Assembly.FullName;

    /// <summary>Sends the not-yet-sent definitions in the batch to native. Never throws.</summary>
    public CoordinatorResult Register(CoordinatorDefinition[] definitions)
    {
        if (definitions == null)
        {
            return new CoordinatorResult(CoordinatorOutcome.Failed, 0, 0, 0, null, "no definitions");
        }

        foreach (var definition in definitions)
        {
            if (definition == null)
            {
                return new CoordinatorResult(CoordinatorOutcome.Failed, definitions.Length, 0, 0, null, "null definition");
            }
        }

        lock (_lock)
        {
            var fresh = new List<CoordinatorDefinition>();
            var batchKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                if (!_sent.Contains(definition.Key) && batchKeys.Add(definition.Key))
                {
                    fresh.Add(definition);
                }
            }

            if (fresh.Count == 0)
            {
                return new CoordinatorResult(CoordinatorOutcome.AllPresent, definitions.Length, 0, 0, null, null);
            }

            var id = "NR-" + _processId + "-" + _nextId++;
            int taken;
            try
            {
                taken = _native.Register(id, fresh.ToArray(), uint.MaxValue);
            }
            catch (Exception ex)
            {
                return new CoordinatorResult(CoordinatorOutcome.Failed, definitions.Length, 0, 0, id, ex.GetType().Name + ": " + ex.Message);
            }

            if (taken <= 0)
            {
                return new CoordinatorResult(CoordinatorOutcome.Failed, definitions.Length, 0, taken, id,
                    "OTel native took " + taken + " of " + fresh.Count);
            }

            foreach (var definition in fresh)
            {
                _sent.Add(definition.Key);
            }

            return new CoordinatorResult(CoordinatorOutcome.Sent, definitions.Length, fresh.Count, taken, id, null);
        }
    }

    /// <summary>Lives for the life of the process; remoting must never expire it.</summary>
#pragma warning disable CS0672 // On .NET (Core) the base member is obsolete; .NET Framework remoting is the only consumer of this override.
    public override object InitializeLifetimeService()
#pragma warning restore CS0672
    {
        return null;
    }
}
