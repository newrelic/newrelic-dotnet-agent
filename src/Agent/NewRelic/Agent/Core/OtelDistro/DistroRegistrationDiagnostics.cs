// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Logs what <see cref="DistroRegistrationRouter"/> decided about registering NR instrumentation with OTel. Stateless:
/// the router tracks what was already logged. Never throws.
/// </summary>
public static class DistroRegistrationDiagnostics
{
    public static void ReportCoordinatorInUse(string location)
    {
        try
        {
            Log.Info("OTel distro registration coordinator in use ({0}).", location);
        }
        catch (Exception)
        {
            // Diagnostics must never reach OTel start-up, the app, or a CLR callback.
        }
    }

    public static void ReportCoordinatorUnavailable(string reason)
    {
        try
        {
            Log.Info("OTel distro registration coordinator unavailable ({0}); this AppDomain registers with OTel on its own.", reason);
        }
        catch (Exception)
        {
        }
    }

    public static void ReportStartupRegistered(int requested, int sent)
    {
        try
        {
            Log.Info("NR instrumentation registered through the coordinator at start-up: {0} requested, {1} sent, {2} already registered in this process.", requested, sent, requested - sent);
        }
        catch (Exception)
        {
        }
    }

    public static void ReportStartupFallback(string reason)
    {
        try
        {
            Log.Info("NR instrumentation could not be registered through the coordinator at start-up ({0}); using OTel's start-up payload.", reason);
        }
        catch (Exception)
        {
        }
    }

    public static void ReportRunTimeAlreadyRegistered(int requested)
    {
        try
        {
            Log.Info("NR instrumentation for a live XML change was already registered in this process ({0} requested); no OTel call.", requested);
        }
        catch (Exception)
        {
        }
    }

    public static void ReportRunTimeCoordinatorUnavailable(string reason)
    {
        try
        {
            Log.Info("NR instrumentation could not be registered through the coordinator ({0}); this AppDomain registers with OTel on its own from now on.", reason);
        }
        catch (Exception)
        {
        }
    }
}
