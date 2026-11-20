// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// New Relic's OTel environment defaults in distro mode. Must run before OTel auto-instrumentation reads its settings,
/// so the plugin calls it in <c>Initializing()</c> right after Core start, and also when Core start failed (OTel reads
/// the exporter and instrumentation settings after <c>Initializing()</c> returns). It:
/// <list type="bullet">
/// <item>sets <c>OTEL_TRACES_EXPORTER</c>, <c>OTEL_METRICS_EXPORTER</c> and <c>OTEL_LOGS_EXPORTER</c> to <c>none</c>
/// unconditionally, warning (and counting) when a signal's prior value was something else;</item>
/// <item>leaves <c>OTEL_PROPAGATORS</c> alone (<c>none</c> is not valid there), warning (and counting) when the customer
/// set it to anything other than the default <c>tracecontext,baggage</c>;</item>
/// <item>sets <c>OTEL_DOTNET_AUTO_TRACES_&lt;ID&gt;_INSTRUMENTATION_ENABLED=false</c> for each given ID only when it is
/// unset (null). Any present value, including an empty one, is the customer's and is kept; a kept value OTel does not
/// read as a boolean is logged as a Warning, because OTel then treats the setting as unset (or fails start-up under
/// fail-fast).</item>
/// <item>does NOT write <c>OTEL_DOTNET_AUTO_LOGS_ENABLE_NLOG_BRIDGE</c>: OTel's NLog bridge is opt-in and already
/// defaults to off, and writing the variable would leak into child processes as a spurious customer value. Instead, when
/// New Relic's NLog instrumentation is compiled (<c>nrNLogInstrumentationCompiled</c>) and the customer has turned the
/// bridge on, it logs a Warning (and counts <c>Config/NLogBridgeKeptOn</c>) that NLog lines may be forwarded twice --
/// OTel's NLog bridge emits before New Relic's inner hook, so the per-call log dedupe cannot catch it. The NLOG trace
/// instrumentation ID is never touched (OTel keeps NLog trace-context injection); the log4net and ILogger bridges are
/// left to the per-call dedupe.</item>
/// </list>
/// When OTel's file-based (YAML) configuration is active (<c>OTEL_EXPERIMENTAL_FILE_BASED_CONFIGURATION_ENABLED</c> is
/// exactly <c>true</c>, as OTel's <c>Settings.cs</c> reads it) OTel ignores all of these variables. Everything is still
/// written, but the per-variable lines, the exporter and propagator Warnings and their metrics are replaced by one
/// Warning (the YAML case is reported as an Error, with its own metric, once OTel has started).
/// Never sets <c>OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED</c> and never touches the trace/log/metrics
/// instrumentation IDs other than the trace disables above. Never throws: the first failure is logged as one Error, kept
/// in <see cref="DistroOtelDefaultsResult.FailureMessage"/>, and the remaining steps are still attempted. A failing logger
/// never stops an environment write (Core's logger may be unusable when Core failed to start; the plugin then logs
/// <see cref="FormatSummary"/> to its own log).
/// </summary>
public static class DistroOtelEnvironmentDefaults
{
    public const string ExporterNotNoneMetricPrefix = "Supportability/Dotnet/OtelDistro/Config/ExporterNotNone/";
    public const string PropagatorsNotDefaultMetricName = "Supportability/Dotnet/OtelDistro/Config/PropagatorsNotDefault";
    public const string NLogBridgeKeptOnMetricName = "Supportability/Dotnet/OtelDistro/Config/NLogBridgeKeptOn";

    private const string PropagatorsVariable = "OTEL_PROPAGATORS";

    // OTel's opt-in NLog bridge switch (Configurations/ConfigurationKeys.cs:305). Default false. New Relic never writes
    // it (a write would leak to child processes as a customer value); it only reads it to warn about duplicate NLog lines.
    private const string NLogBridgeVariable = "OTEL_DOTNET_AUTO_LOGS_ENABLE_NLOG_BRIDGE";

    // OTel reads this once, before plugins load: Configurations/Settings.cs:15, == "true" (ordinal, case-sensitive, untrimmed).
    private const string FileBasedConfigVariable = "OTEL_EXPERIMENTAL_FILE_BASED_CONFIGURATION_ENABLED";
    private const string DefaultPropagators = "tracecontext,baggage";
    private const string NoneExporter = "none";
    private const string FailurePrefix = "OTel distro: could not apply New Relic's OTel environment defaults; OTel may export or instrument where New Relic expected not to: {0}";

    private static readonly KeyValuePair<string, string>[] ExporterVariables =
    {
        new KeyValuePair<string, string>("Traces", "OTEL_TRACES_EXPORTER"),
        new KeyValuePair<string, string>("Metrics", "OTEL_METRICS_EXPORTER"),
        new KeyValuePair<string, string>("Logs", "OTEL_LOGS_EXPORTER"),
    };

    public static DistroOtelDefaultsResult Apply(IDistroEnvironmentVariables env, IReadOnlyList<string> disableIds, bool nrNLogInstrumentationCompiled)
    {
        var appliedDisables = new List<string>();
        var customerValuesKept = new List<KeyValuePair<string, string>>();
        var exportersOverridden = new List<KeyValuePair<string, string>>();
        string nonDefaultPropagators = null;
        string failureMessage = null;

        if (env == null)
        {
            RecordFailure(ref failureMessage, "no environment to apply them to");
            return new DistroOtelDefaultsResult(appliedDisables, customerValuesKept, exportersOverridden, null, failureMessage, false);
        }

        var fileBasedConfigActive = false;
        try
        {
            fileBasedConfigActive = env.Get(FileBasedConfigVariable) == "true";
        }
        catch (Exception ex)
        {
            RecordFailure(ref failureMessage, ex.Message);
        }

        foreach (var exporter in ExporterVariables)
        {
            try
            {
                var prior = env.Get(exporter.Value);
                env.Set(exporter.Value, NoneExporter);
                if (!string.IsNullOrEmpty(prior) && !string.Equals(prior.Trim(), NoneExporter, StringComparison.OrdinalIgnoreCase))
                {
                    exportersOverridden.Add(new KeyValuePair<string, string>(exporter.Key, prior));
                    if (fileBasedConfigActive)
                    {
                        // OTel ignores this value in YAML mode; the YAML-effective exporters are reported after start-up.
                        continue;
                    }

                    TryLog(() => Log.Warn("OTel distro: {0} was '{1}'; New Relic set it to 'none' (New Relic exports this signal).", exporter.Value, prior));
                    DistroConfigMetrics.Record(ExporterNotNoneMetricPrefix + exporter.Key);
                }
            }
            catch (Exception ex)
            {
                RecordFailure(ref failureMessage, ex.Message);
            }
        }

        try
        {
            var propagators = env.Get(PropagatorsVariable);
            if (!string.IsNullOrWhiteSpace(propagators) && !IsDefaultPropagators(propagators))
            {
                nonDefaultPropagators = propagators;

                // OTel ignores this value in YAML mode.
                if (!fileBasedConfigActive)
                {
                    TryLog(() => Log.Warn("OTel distro: OTEL_PROPAGATORS is '{0}' (not the default tracecontext,baggage); kept as set. New Relic propagates through its own instrumentation; OTel's propagators apply only where OTel instruments.", propagators));
                    DistroConfigMetrics.Record(PropagatorsNotDefaultMetricName);
                }
            }
        }
        catch (Exception ex)
        {
            RecordFailure(ref failureMessage, ex.Message);
        }

        if (disableIds != null)
        {
            foreach (var id in disableIds)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                try
                {
                    var name = "OTEL_DOTNET_AUTO_TRACES_" + id + "_INSTRUMENTATION_ENABLED";
                    var customerValue = env.Get(name);
                    if (customerValue == null)
                    {
                        env.Set(name, "false");
                        appliedDisables.Add(id);
                        if (!fileBasedConfigActive)
                        {
                            TryLog(() => Log.Info("OTel distro: set {0}=false so New Relic instruments this library (the customer had not set it).", name));
                        }
                    }
                    else
                    {
                        customerValuesKept.Add(new KeyValuePair<string, string>(id, customerValue));
                        if (fileBasedConfigActive)
                        {
                            continue;
                        }

                        if (IsOtelBoolean(customerValue))
                        {
                            TryLog(() => Log.Info("OTel distro: kept the customer's {0}='{1}'; New Relic does not override it.", name, customerValue));
                        }
                        else
                        {
                            TryLog(() => Log.Warn("OTel distro: kept the customer's {0}='{1}'; New Relic does not override it, but OpenTelemetry does not read '{1}' as true or false: it treats the setting as unset, so this OTel instrumentation stays on (unless OTEL_DOTNET_AUTO_TRACES_INSTRUMENTATION_ENABLED=false), or, with OTEL_DOTNET_AUTO_FAIL_FAST=true, OpenTelemetry fails to start. Set it to true or false.", name, customerValue));
                        }
                    }
                }
                catch (Exception ex)
                {
                    RecordFailure(ref failureMessage, ex.Message);
                }
            }
        }

        // Diagnostic only -- never writes the NLog bridge variable. When New Relic's NLog instrumentation is compiled and
        // the customer has turned OTel's (opt-in, default-off) NLog bridge on, warn that NLog lines may be forwarded twice
        // (the per-call dedupe cannot catch OTel's NLog bridge, which emits before New Relic's inner hook). In YAML mode
        // the env var has no effect, so skip it there (the YAML-effective config is reported after start-up).
        if (nrNLogInstrumentationCompiled && !fileBasedConfigActive)
        {
            try
            {
                var customerValue = env.Get(NLogBridgeVariable);
                if (IsOtelTrue(customerValue))
                {
                    var kept = customerValue;
                    TryLog(() => Log.Warn("OTel distro: OTEL_DOTNET_AUTO_LOGS_ENABLE_NLOG_BRIDGE='{0}' turns OpenTelemetry's NLog bridge on while New Relic's NLog instrumentation also forwards NLog logs, so each NLog line may be forwarded twice (duplicate logs). New Relic does not change the setting; set it to false to forward each NLog line once.", kept));
                    DistroConfigMetrics.Record(NLogBridgeKeptOnMetricName);
                }
            }
            catch (Exception ex)
            {
                RecordFailure(ref failureMessage, ex.Message);
            }
        }

        if (fileBasedConfigActive)
        {
            var disabled = appliedDisables.Count == 0 ? "none" : string.Join(", ", appliedDisables);
            TryLog(() => Log.Warn("OTel distro: OTEL_EXPERIMENTAL_FILE_BASED_CONFIGURATION_ENABLED=true, so OpenTelemetry ignores its OTEL_* environment variables; New Relic's OTel environment defaults (exporters none; OTel trace instrumentation off: {0}) were written but have no effect. See the file-based configuration error reported after OpenTelemetry start-up.", disabled));
        }

        return new DistroOtelDefaultsResult(appliedDisables, customerValuesKept, exportersOverridden, nonDefaultPropagators, failureMessage, fileBasedConfigActive);
    }

    /// <summary>
    /// One line describing what <see cref="Apply"/> did, for a log other than Core's (the plugin's own log when Core failed
    /// to start). Never throws; a null result yields a fixed line.
    /// </summary>
    public static string FormatSummary(DistroOtelDefaultsResult result)
    {
        if (result == null)
        {
            return "OTel environment defaults: no result.";
        }

        var overridden = result.ExportersOverridden.Count == 0
            ? "none overridden"
            : "overridden: " + string.Join(", ", result.ExportersOverridden.Select(e => e.Key + "='" + e.Value + "'"));
        var disabled = result.AppliedDisables.Count == 0 ? "none" : string.Join(", ", result.AppliedDisables);
        var kept = result.CustomerValuesKept.Count == 0
            ? "none"
            : string.Join(", ", result.CustomerValuesKept.Select(k => k.Key + "='" + k.Value + "'"));
        var propagators = result.NonDefaultPropagators == null ? "default" : "'" + result.NonDefaultPropagators + "' (not default, kept)";
        var tail = "OTel trace instrumentation disabled: " + disabled + "; customer values kept: " + kept
            + "; OTEL_PROPAGATORS: " + propagators + ".";

        // A failed step means an exporter (or an instrumentation disable) may not have been written, so the line must not
        // claim exporters are none; overridden/disabled/kept list only what was recorded after a successful write.
        var yaml = result.FileBasedConfigActive ? "YAML mode: OTel ignores these env defaults; " : string.Empty;
        return result.FailureMessage == null
            ? "OTel environment defaults: " + yaml + "exporters set to none (" + overridden + "); " + tail
            : "OTel environment defaults: " + yaml + "NOT ALL APPLIED (FAILED: " + result.FailureMessage + "); exporters may not be set to none (" + overridden + "); " + tail;
    }

    // OTel reads a per-ID switch with bool.Parse (fail-fast) or bool.TryParse, falling back to the global switch when the
    // value is not a boolean (StringConfigurationSource.GetBool, Configurations/StringConfigurationSource.cs:64-80;
    // ConfigurationExtensions.ParseEnabledEnumList, :29). Same runtime, same parser.
    private static bool IsOtelBoolean(string value)
    {
        return bool.TryParse(value, out _);
    }

    // OTel reads the NLog bridge switch with GetBool, which is bool.TryParse with a false fallback (a non-boolean reads
    // as off). The duplicate-logs risk is only a value that reads as true.
    private static bool IsOtelTrue(string value)
    {
        return bool.TryParse(value, out var parsed) && parsed;
    }

    private static bool IsDefaultPropagators(string value)
    {
        var compact = value.Replace(" ", string.Empty).Replace("\t", string.Empty);
        return string.Equals(compact, DefaultPropagators, StringComparison.OrdinalIgnoreCase);
    }

    private static void RecordFailure(ref string failureMessage, string message)
    {
        if (failureMessage != null)
        {
            return;
        }

        failureMessage = message;
        TryLog(() => Log.Error(FailurePrefix, message));
    }

    private static void TryLog(Action log)
    {
        try
        {
            log();
        }
        catch (Exception)
        {
            // Core's logger may be unusable (Core failed to start); the environment writes matter more than the log line.
        }
    }
}
