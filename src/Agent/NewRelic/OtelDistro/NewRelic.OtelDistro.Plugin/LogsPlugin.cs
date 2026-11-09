// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using NewRelic.Agent.Core.OtelDistro;
using NewRelic.Agent.Extensions.Logging;
using OpenTelemetry.AutoInstrumentation.PluginApi;
using OpenTelemetry.AutoInstrumentation.PluginApi.Telemetry;
using OpenTelemetry.Logs;

namespace NewRelic.OtelDistro.Plugin;

/// <summary>
/// OpenTelemetry .NET auto-instrumentation plugin (OTEL_DOTNET_AUTO_PLUGINS) that attaches a processor to the
/// application's OTel logger provider and feeds its records into Core's <see cref="DistroLogSink"/>.
/// It never starts Core: <see cref="Plugin"/> does, and this plugin does nothing if the distro agent is not running.
/// </summary>
public class LogsPlugin : IPlugin, IConfigureLogsOptions<OpenTelemetryLoggerOptions>
{
    private const string LogPrefix = "[NewRelic.OtelDistro.Plugin]";

    private static int _loggedError;

    public void Initializing()
    {
    }

    public void Initialized()
    {
    }

    public void ConfigureLogsOptions(OpenTelemetryLoggerOptions options)
    {
        // Upstream calls this with no try/catch, and an exception would surface in the app's LoggingBuilder constructor.
        // The body lives in a separate non-inlined method: it touches Core types (DistroAgentHost, DistroLogProcessor), and the
        // JIT resolves those when it compiles the method that uses them. If NewRelic.Agent.Core cannot be loaded, that failure
        // has to happen inside this try, not while compiling this method.
        try
        {
            AttachProcessor(options);
        }
        catch (Exception ex)
        {
            if (Interlocked.Exchange(ref _loggedError, 1) == 0)
            {
                Console.Error.WriteLine($"{LogPrefix} Configuring logs failed; log forwarding is not attached: {ex}");
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AttachProcessor(OpenTelemetryLoggerOptions options)
    {
        var agent = DistroAgentHost.Current;
        if (agent == null || !agent.LogSink.Enabled)
        {
            Log.Debug("OTel distro logs plugin: ConfigureLogsOptions called; not attaching a log processor (distro agent not running or log sink disabled).");
            return;
        }

        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;
        options.AddProcessor(new DistroLogProcessor(agent.LogSink));
        Log.Debug("OTel distro logs plugin: ConfigureLogsOptions called; attached a log processor.");
    }
}
