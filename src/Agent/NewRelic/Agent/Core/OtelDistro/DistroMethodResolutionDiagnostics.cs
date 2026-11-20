// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Reports the distro method-resolution fallbacks (<see cref="DistroResolutionOutcome.AmbiguousDisagreeing"/> and
/// <see cref="DistroResolutionOutcome.NotFound"/>): one Info log per site function id and outcome per AppDomain, and a
/// supportability count metric per report. Counts go through <see cref="DistroConfigMetrics"/>, the one distro metric
/// queue (held until the agent connects). Resolution can run before Core has started, when the logger may still be the
/// no-op one, so log lines are held until <see cref="SetLoggerReady"/> (Core start), independently of the metrics. Never throws.
/// </summary>
public static class DistroMethodResolutionDiagnostics
{
    public const string AmbiguousMetricName = "Supportability/Dotnet/OtelDistro/MethodResolution/Ambiguous";
    public const string NotFoundMetricName = "Supportability/Dotnet/OtelDistro/MethodResolution/NotFound";

    private static readonly object Gate = new object();
    private static readonly HashSet<(ulong, DistroResolutionOutcome)> Logged = new HashSet<(ulong, DistroResolutionOutcome)>();
    private static readonly List<PendingLog> PendingLogs = new List<PendingLog>();

    private static bool _loggerReady;

    /// <summary>
    /// Reports the outcome of one resolution. <see cref="DistroResolutionOutcome.Resolved"/> and
    /// <see cref="DistroResolutionOutcome.AmbiguousAgreeing"/> report nothing.
    /// </summary>
    public static void Report(DistroResolvedMethod result, DistroInstrumentationSite site, int parameterCount)
    {
        try
        {
            string metricName;
            switch (result.Outcome)
            {
                case DistroResolutionOutcome.AmbiguousDisagreeing:
                    metricName = AmbiguousMetricName;
                    break;
                case DistroResolutionOutcome.NotFound:
                    metricName = NotFoundMetricName;
                    break;
                default:
                    return;
            }

            // Metric first, so a failing logger cannot drop it.
            DistroConfigMetrics.Record(metricName);

            bool logIt;
            lock (Gate)
            {
                logIt = Logged.Add((site.FunctionId, result.Outcome));

                // Core has not started, so the logger may still be the no-op one and the line would be lost for good
                // (it is logged once). Hold it until Core's logger is ready.
                if (logIt && !_loggerReady)
                {
                    PendingLogs.Add(new PendingLog(result.Outcome, site.TypeName, site.MethodName, parameterCount));
                    logIt = false;
                }
            }

            if (logIt)
            {
                LogFallback(new PendingLog(result.Outcome, site.TypeName, site.MethodName, parameterCount));
            }
        }
        catch (Exception)
        {
            // Diagnostics must never reach OTel start-up or an instrumented method.
        }
    }

    /// <summary>
    /// Marks Core's logger ready (true: flushes the held log lines, later lines are logged directly) or not ready (false,
    /// at shutdown: later lines are held again). Never throws.
    /// </summary>
    public static void SetLoggerReady(bool ready)
    {
        PendingLog[] logs;
        lock (Gate)
        {
            _loggerReady = ready;
            if (!ready)
            {
                return;
            }

            logs = PendingLogs.ToArray();
            PendingLogs.Clear();
        }

        try
        {
            foreach (var pending in logs)
            {
                LogFallback(pending);
            }
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Test seam: forgets what was logged and held, and marks the logger not ready.</summary>
    public static void ResetForTesting()
    {
        lock (Gate)
        {
            Logged.Clear();
            _loggerReady = false;
            PendingLogs.Clear();
        }
    }

    private static void LogFallback(PendingLog pending)
    {
        if (pending.Outcome == DistroResolutionOutcome.AmbiguousDisagreeing)
        {
            Log.Info("OTel distro: cannot tell overloads of {0}.{1} with {2} parameters apart and they differ in async-ness; treating the call as not async (deliberate fallback).", pending.TypeName, pending.MethodName, pending.ParameterCount);
        }
        else
        {
            Log.Info("OTel distro: could not identify the method {0}.{1} with {2} parameters that fired; treating the call as not async.", pending.TypeName, pending.MethodName, pending.ParameterCount);
        }
    }

    private readonly struct PendingLog
    {
        public readonly DistroResolutionOutcome Outcome;
        public readonly string TypeName;
        public readonly string MethodName;
        public readonly int ParameterCount;

        public PendingLog(DistroResolutionOutcome outcome, string typeName, string methodName, int parameterCount)
        {
            Outcome = outcome;
            TypeName = typeName;
            MethodName = methodName;
            ParameterCount = parameterCount;
        }
    }
}
