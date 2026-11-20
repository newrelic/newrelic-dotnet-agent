// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NewRelic.Agent.Core.OtelDistro;
using NewRelic.OtelDistro.Coordinator;

namespace NewRelic.OtelDistro.Plugin;

/// <summary>
/// Adapts the process-wide <see cref="RegistrationCoordinator"/> to Core's <see cref="IDistroSiteRegistrar"/>: site i is
/// definitions[i] of the generated table. Only this type references the coordinator assembly; the plugin reaches it
/// through a non-inlined, guarded helper so a missing coordinator DLL never fails the plugin's own type load.
/// </summary>
internal sealed class CoordinatorSiteRegistrar : IDistroSiteRegistrar
{
    // Same values as DistroDefinitionsPayload: CallTargetKind.Default (direct) and the tracing category.
    private const byte DirectCallTargetKind = 0;
    private const uint TracingCategory = 1;

    private readonly RegistrationCoordinator _coordinator;
    private readonly IReadOnlyList<DistroCallTargetDefinition> _definitions;
    private readonly string _integrationAssembly;

    private CoordinatorSiteRegistrar(RegistrationCoordinator coordinator, IReadOnlyList<DistroCallTargetDefinition> definitions, string integrationAssembly)
    {
        _coordinator = coordinator;
        _definitions = definitions;
        _integrationAssembly = integrationAssembly;
    }

    /// <summary>
    /// The registrar over the process's coordinator, or null with <paramref name="failure"/> set when there is none.
    /// Throws only when the coordinator assembly itself cannot be loaded (the caller turns that into a reason).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static IDistroSiteRegistrar TryCreate(IReadOnlyList<DistroCallTargetDefinition> definitions, string integrationAssembly, out string location, out string failure)
    {
        var coordinator = CoordinatorHost.TryGet(out location, out failure);
        return coordinator == null ? null : new CoordinatorSiteRegistrar(coordinator, definitions, integrationAssembly);
    }

    /// <summary>Where this AppDomain loaded the coordinator assembly from, or null. Never throws.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static string AssemblyLocation()
    {
        try
        {
            return typeof(RegistrationCoordinator).Assembly.Location;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public DistroSiteRegistration Register(IReadOnlyList<int> sites)
    {
        try
        {
            var batch = new CoordinatorDefinition[sites.Count];
            for (var i = 0; i < batch.Length; i++)
            {
                var d = _definitions[sites[i]];
                batch[i] = new CoordinatorDefinition(
                    d.TargetAssembly,
                    d.TargetType,
                    d.TargetMethod,
                    d.TargetSignatureTypes,
                    d.MinMajor,
                    d.MinMinor,
                    d.MinPatch,
                    d.MaxMajor,
                    d.MaxMinor,
                    d.MaxPatch,
                    _integrationAssembly,
                    d.IntegrationTypeName,
                    DirectCallTargetKind,
                    TracingCategory);
            }

            var result = _coordinator.Register(batch);
            if (result == null)
            {
                return new DistroSiteRegistration(DistroSiteRegistrationOutcome.Unavailable, batch.Length, 0, "the registration coordinator returned no result");
            }

            return new DistroSiteRegistration(Map(result.Outcome), result.Requested, result.Sent, result.Failure);
        }
        catch (Exception ex)
        {
            // AppDomainUnloadedException / RemotingException (the default domain's coordinator is gone), or anything else.
            return new DistroSiteRegistration(DistroSiteRegistrationOutcome.Unavailable, sites?.Count ?? 0, 0, ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static DistroSiteRegistrationOutcome Map(CoordinatorOutcome outcome)
    {
        switch (outcome)
        {
            case CoordinatorOutcome.Sent:
                return DistroSiteRegistrationOutcome.Sent;
            case CoordinatorOutcome.AllPresent:
                return DistroSiteRegistrationOutcome.AllPresent;
            default:
                return DistroSiteRegistrationOutcome.Failed;
        }
    }
}
