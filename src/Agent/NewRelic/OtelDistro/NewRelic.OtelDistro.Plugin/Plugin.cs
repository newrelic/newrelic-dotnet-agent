// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.OtelDistro;
using NewRelic.Agent.Core.Utilities;
using OpenTelemetry.AutoInstrumentation.PluginApi;

namespace NewRelic.OtelDistro.Plugin;

/// <summary>
/// OpenTelemetry .NET auto-instrumentation plugin (OTEL_DOTNET_AUTO_PLUGINS) that starts New Relic Core
/// without the NR profiler and feeds it the application's Activities.
/// </summary>
public class Plugin : IPlugin
{
    private const string LogPrefix = "[NewRelic.OtelDistro.Plugin]";

    private static readonly object StateLock = new object();

    // Keep-alive reference: prevents the listener from being garbage collected for the process lifetime.
    private static DistroActivityListener _listener;

    // Keep-alive reference for the EventBus subscription (the bus holds the delegate, this also lets us unsubscribe it).
    private static Action<AgentConnectedEvent> _connectedHandler;

    private static IDistroAgent _agent;
    private static bool _connected;
    private static bool _customEventRecorded;

    public void Initializing()
    {
        try
        {
            // Subscribe BEFORE starting: a synchronous connect (send-data-on-exit / sync startup) publishes
            // AgentConnectedEvent from inside Start(), and an async connect can finish before Start() returns.
            SubscribeToConnected();

            var distroAgent = DistroAgentHost.Start();

            // Same gate as agent mode: no listener unless openTelemetry.enabled and openTelemetry.traces.enabled.
            if (distroAgent.ActivitySink.TracingEnabled)
            {
                var listener = new DistroActivityListener(distroAgent.ActivitySink);
                listener.Start();
                _listener = listener;
            }
            else
            {
                Log("OpenTelemetry tracing disabled by configuration; listener not attached.");
            }

            LogDiagnostics(distroAgent);

            lock (StateLock)
            {
                _agent = distroAgent;
            }

            // Covers a connect that already happened before the agent was available to the handler.
            TryRecordCustomEvent();
        }
        catch (Exception ex)
        {
            UnsubscribeFromConnected();

            // Core does not log a start-up failure in distro mode, so this is the only place it surfaces.
            Log($"Start-up failed: {ex}");
        }
    }

    public void Initialized()
    {
    }

    private static void SubscribeToConnected()
    {
        Action<AgentConnectedEvent> handler;
        lock (StateLock)
        {
            if (_customEventRecorded || _connectedHandler != null)
            {
                return;
            }

            handler = OnAgentConnected;
            _connectedHandler = handler;
        }

        EventBus<AgentConnectedEvent>.Subscribe(handler);
    }

    private static void UnsubscribeFromConnected()
    {
        Action<AgentConnectedEvent> handler;
        lock (StateLock)
        {
            handler = _connectedHandler;
            _connectedHandler = null;
        }

        if (handler != null)
        {
            EventBus<AgentConnectedEvent>.Unsubscribe(handler);
        }
    }

    // Runs on the connection thread. Must never throw into the EventBus.
    private static void OnAgentConnected(AgentConnectedEvent agentConnectedEvent)
    {
        try
        {
            lock (StateLock)
            {
                _connected = true;
            }

            TryRecordCustomEvent();
        }
        catch (Exception ex)
        {
            Log($"Custom event failed: {ex}");
        }
    }

    // Records the custom event exactly once, after the agent is both started and connected. The aggregator discards
    // anything recorded before the connect (it resets on the server configuration update), so recording earlier loses the event.
    private static void TryRecordCustomEvent()
    {
        try
        {
            IDistroAgent agent;
            lock (StateLock)
            {
                if (_customEventRecorded || !_connected || _agent == null)
                {
                    return;
                }

                _customEventRecorded = true;
                agent = _agent;
            }

            UnsubscribeFromConnected();

            agent.AgentApi.RecordCustomEvent("OtelDistroPrototype", new[] { new KeyValuePair<string, object>("source", "s4-plugin") });
        }
        catch (Exception ex)
        {
            Log($"Custom event failed: {ex}");
        }
    }

    // Diagnostics only: a failure here must not be reported as a start-up failure, the agent is already running.
    private static void LogDiagnostics(IDistroAgent distroAgent)
    {
        try
        {
            var extensionsDirectory = distroAgent.ExtensionsDirectory;
            var xmlCount = !string.IsNullOrEmpty(extensionsDirectory) && Directory.Exists(extensionsDirectory)
                ? Directory.GetFiles(extensionsDirectory, "*.xml").Length
                : 0;
            Log($"Started. Loaded wrappers: {distroAgent.LoadedWrapperCount}; extensions directory: {extensionsDirectory}; extension xml files: {xmlCount}");
        }
        catch (Exception ex)
        {
            Log($"Diagnostic logging failed: {ex}");
        }
    }

    private static void Log(string message)
    {
        Console.Error.WriteLine($"{LogPrefix} {message}");
    }
}
