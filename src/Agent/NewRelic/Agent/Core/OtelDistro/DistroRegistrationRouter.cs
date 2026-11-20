// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Decides how this AppDomain registers NR instrumentation sites with OTel: through the process-wide coordinator
/// (<see cref="IDistroSiteRegistrar"/>) when there is one, otherwise through the fallback, today's per-AppDomain
/// registration. One router per AppDomain; the "once" log lines are once per router. Never throws.
/// </summary>
public sealed class DistroRegistrationRouter
{
    private const int Done = 1;

    private readonly IDistroSiteRegistrar _coordinator;
    private readonly string _location;
    private readonly string _unavailableReason;
    private readonly DistroRegisterSites _fallback;

    private int _inUseLogged;
    private int _unavailableLogged;
    private int _coordinatorLost;

    /// <param name="coordinator">The coordinator, or null when none could be found or created (then every call falls back).</param>
    /// <param name="location">Where the coordinator lives, for the log.</param>
    /// <param name="unavailableReason">Why there is no coordinator (used when <paramref name="coordinator"/> is null), for the log.</param>
    /// <param name="fallback">This AppDomain's own run-time registration with OTel.</param>
    public DistroRegistrationRouter(IDistroSiteRegistrar coordinator, string location, string unavailableReason, DistroRegisterSites fallback)
    {
        _coordinator = coordinator;
        _location = location;
        _unavailableReason = unavailableReason;
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
    }

    /// <summary>
    /// Registers <paramref name="sites"/> at start-up. True: the coordinator handled it (sent them, or they were already
    /// registered in the process) and OTel's start-up payload must not carry them. False: the caller uses OTel's start-up
    /// payload. This never calls the run-time fallback: start-up fallback is OTel's own hook payload. A start-up outcome
    /// that is not a success (failed, unavailable, or the coordinator threw) switches this router's run-time path to the
    /// fallback for good.
    /// </summary>
    public bool RegisterAtStartup(IReadOnlyList<int> sites)
    {
        if (_coordinator == null)
        {
            LogUnavailableOnce();
            return false;
        }

        if (Interlocked.Exchange(ref _inUseLogged, Done) != Done)
        {
            DistroRegistrationDiagnostics.ReportCoordinatorInUse(_location);
        }

        var result = Call(sites);
        switch (result.Outcome)
        {
            case DistroSiteRegistrationOutcome.Sent:
            case DistroSiteRegistrationOutcome.AllPresent:
                DistroRegistrationDiagnostics.ReportStartupRegistered(result.Requested, result.Sent);
                return true;
            default:
                // The start-up call already fell back to OTel's own hook payload, so live edits must use the matching
                // per-AppDomain path for good, not a coordinator that just failed (same latch as a run-time Unavailable).
                Volatile.Write(ref _coordinatorLost, Done);
                DistroRegistrationDiagnostics.ReportStartupFallback(result.Failure ?? result.Outcome.ToString());
                return false;
        }
    }

    /// <summary>
    /// Registers <paramref name="sites"/> for a live XML change. Same contract as <see cref="DistroRegisterSites"/>: false
    /// with a <paramref name="failure"/> leaves the sites off. A coordinator failure is not retried through the fallback
    /// (the gate stays off, as today); a coordinator that cannot be used switches this router to the fallback for good.
    /// </summary>
    public bool RegisterAtRunTime(IReadOnlyList<int> sites, out string failure)
    {
        if (_coordinator == null)
        {
            LogUnavailableOnce();
            return CallFallback(sites, out failure);
        }

        if (Volatile.Read(ref _coordinatorLost) == Done)
        {
            return CallFallback(sites, out failure);
        }

        var result = Call(sites);
        switch (result.Outcome)
        {
            case DistroSiteRegistrationOutcome.Sent:
                failure = null;
                return true;
            case DistroSiteRegistrationOutcome.AllPresent:
                DistroRegistrationDiagnostics.ReportRunTimeAlreadyRegistered(result.Requested);
                failure = null;
                return true;
            case DistroSiteRegistrationOutcome.Failed:
                failure = result.Failure ?? "the registration coordinator reported a failure";
                return false;
            default:
                if (Interlocked.Exchange(ref _coordinatorLost, Done) != Done)
                {
                    DistroRegistrationDiagnostics.ReportRunTimeCoordinatorUnavailable(result.Failure ?? result.Outcome.ToString());
                }

                return CallFallback(sites, out failure);
        }
    }

    // A coordinator that throws, or returns nothing, is treated as unavailable.
    private DistroSiteRegistration Call(IReadOnlyList<int> sites)
    {
        try
        {
            return _coordinator.Register(sites)
                ?? new DistroSiteRegistration(DistroSiteRegistrationOutcome.Unavailable, 0, 0, "the registration coordinator returned no result");
        }
        catch (Exception ex)
        {
            return new DistroSiteRegistration(DistroSiteRegistrationOutcome.Unavailable, 0, 0, ex.GetType().Name + ": " + ex.Message);
        }
    }

    private bool CallFallback(IReadOnlyList<int> sites, out string failure)
    {
        try
        {
            return _fallback(sites, out failure);
        }
        catch (Exception ex)
        {
            failure = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    private void LogUnavailableOnce()
    {
        if (Interlocked.Exchange(ref _unavailableLogged, Done) != Done)
        {
            DistroRegistrationDiagnostics.ReportCoordinatorUnavailable(_unavailableReason);
        }
    }
}
