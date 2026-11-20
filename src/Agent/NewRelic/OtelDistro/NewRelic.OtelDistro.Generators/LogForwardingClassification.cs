// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.OtelDistro.Generators;

/// <summary>
/// The one place that says which NR instrumentation points record a log line ("log-forwarding" points), judged by the
/// point's tracer-factory name as spelled in the shipped wrapper XML. In distro mode OTel's own log bridges emit
/// synchronously inside these same methods, so the distro brackets each such call with a same-thread dedupe counter
/// (Core's DistroLogCapture) and drops OTel's duplicate copy; the generator marks the point with the record's
/// FlagLogForwarding bit (see <see cref="TableEntry.LogForwarding"/>).
///
/// Only the methods that record a log line count. The matching tracer-factory names, from the four NR logging wrappers'
/// Instrumentation.xml (src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/{MicrosoftExtensionsLogging,Log4NetLogging,
/// SerilogLogging,NLogLogging}):
///   - MicrosoftLogging      -> Microsoft.Extensions.Logging.Logger.Log (MEL). NOT AddProviderRegistrationWrapper
///                              (LoggerFactory.AddProviderRegistration is logger wiring, not a logged line).
///   - log4net / SitecoreLogging -> log4net.Repository.Hierarchy.Logger.CallAppenders (log4net and the Sitecore fork).
///   - SerilogDispatchWrapper -> Serilog's Dispatch / PostLevelCheckEmit. NOT SerilogCreateLoggerWrapper (logger creation).
///   - nlog                  -> NLog.LoggerImpl.Write.
/// These are compared ordinally and case-sensitively, exactly as the names appear in the XML. The generator cannot
/// reference the wrapper assemblies, so the names are copied here; a rename in a wrapper's Xml would need the matching
/// edit here (pinned by the generator and shipped-XML tests).
/// </summary>
public static class LogForwardingClassification
{
    /// <summary>The tracer-factory name of NR's NLog log-forwarding wrapper.</summary>
    public const string NLogTracerFactoryName = "nlog";

    private static readonly HashSet<string> LogForwardingTracerFactoryNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "MicrosoftLogging",
        "log4net",
        "SitecoreLogging",
        "SerilogDispatchWrapper",
        NLogTracerFactoryName,
    };

    /// <summary>The log-forwarding tracer-factory names, for the tests to pin.</summary>
    public static IReadOnlyCollection<string> LogForwardingTracerFactories => LogForwardingTracerFactoryNames;

    /// <summary>True when a point with tracer factory <paramref name="tracerFactoryName"/> records a log line.</summary>
    public static bool IsLogForwarding(string tracerFactoryName) =>
        tracerFactoryName != null && LogForwardingTracerFactoryNames.Contains(tracerFactoryName);

    /// <summary>True when the point is NR's NLog log-forwarding wrapper (the one OTel's NLog bridge double-captures).</summary>
    public static bool IsNLog(string tracerFactoryName) =>
        string.Equals(tracerFactoryName, NLogTracerFactoryName, StringComparison.Ordinal);
}
