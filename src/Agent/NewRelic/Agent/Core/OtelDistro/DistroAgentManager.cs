// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Commands;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Tracer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Distro-mode counterpart of <see cref="AgentManager"/>: the shared <see cref="AgentManagerBase"/> start-up and
/// shutdown sequence plus distro mode's own steps. Nothing here needs the NR profiler or agent mode's process-wide
/// singletons (AgentManager.Singleton, AgentInitializer.OnExit, ActivityBridge/MeterListenerBridge, thread and
/// continuous profilers, InstrumentationWatcher). Constructed by <see cref="DistroAgentHost"/>.
/// </summary>
public sealed class DistroAgentManager : AgentManagerBase, IDistroAgent, IAgentManager
{
    private int _shutdownStarted;
    private int _disposed;

    // The distro metric connect gate. Guards attaching and detaching the DistroConfigMetrics sink, so a connect that
    // races a shutdown or dispose cannot re-attach it afterwards.
    private readonly object _metricSinkGate = new object();
    private EventSubscription<AgentConnectedEvent> _agentConnectedSubscription;
    private bool _metricSinkClosed;

    // Set once, in AfterWrapperServiceResolved during start-up (step 10); read by GetTracerImpl as a plain
    // field. Null before start reaches step 10, so GetTracerImpl returns null until then.
    private IWrapperService _wrapperService;

    // The XML toggles the plugin asked for (CreateXmlToggles), owned here and disposed on Shutdown/Dispose. The gate keeps a
    // create that races a shutdown or dispose from handing out an instance nobody will dispose.
    private readonly object _xmlTogglesGate = new object();
    private DistroXmlToggles _xmlToggles;
    private bool _xmlTogglesClosed;

    public DistroActivitySink ActivitySink { get; private set; }

    public DistroLogSink LogSink { get; private set; }

    public DistroMetricsSettings Metrics { get; private set; }

    public int LoadedWrapperCount { get; private set; }

    IAgentApi IDistroAgent.AgentApi => AgentApi;

    IConfiguration IDistroAgent.Configuration => Configuration;

    string IDistroAgent.ExtensionsDirectory => ExtensionsDirectory;

    protected override bool DistroModeEnabled => true;

    // ConfigurationService reads NewRelic.Agent.Core.Config.ConfigurationLoader.BootstrapConfig directly (a
    // static, not the value Start() stores), so the wrapped instance has to land there too for
    // DefaultConfiguration to see DistroModeEnabled == true. Agent mode never overrides this, so its
    // IBootstrapConfiguration instance -- and every value read off it -- is unchanged by this method existing.
    protected override IBootstrapConfiguration AdaptBootstrapConfig(IBootstrapConfiguration bootstrapConfig)
    {
        if (bootstrapConfig == null)
        {
            return bootstrapConfig;
        }

        var wrapped = new DistroBootstrapConfiguration(bootstrapConfig);
        ConfigurationLoader.BootstrapConfig = wrapped;
        return wrapped;
    }

    public DistroAgentManager(IAgentManagerEnvironment environment) : base(environment)
    {
        // Agent mode gets this from AgentInitializer.CallOnce before AgentManager is constructed. Without it
        // LoggerBootstrapper has no start-up (in-memory) sink and _isWindows stays false.
        environment.InitializeBootstrapLogger();

        Start();

        // Agent mode's AgentSingleton does this check after the ctor returns; there is no singleton here.
        if (ShutdownEventReceived)
        {
            Shutdown(false);
            throw new Exception("The agent received a shutdown event during start-up.");
        }

        Log.Info("OTel distro mode: Core started via DistroAgentHost (wrappers loaded: {0}, extensions dir: {1})", LoadedWrapperCount, ExtensionsDirectory);
    }

    protected override void AfterWrapperServiceResolved(IWrapperService wrapperService)
    {
        _wrapperService = wrapperService;

        // Step 10: the health reporter exists (step 7) and connect has not been attempted (step 11). A synchronous
        // connect publishes AgentConnectedEvent inside step 11, an asynchronous one later; either way the handler runs.
        _agentConnectedSubscription = new EventSubscription<AgentConnectedEvent>(OnAgentConnected);
    }

    // Distro supportability metrics are held in DistroConfigMetrics until the agent has connected: Core's metric
    // aggregator discards everything recorded before connect (ConnectionHandler publishes configuration updates and
    // MetricAggregator.OnConfigurationUpdated replaces its stats queue), and AgentConnectedEvent is published after
    // those updates. A later reconnect re-attaches harmlessly (the queue is already empty).
    private void OnAgentConnected(AgentConnectedEvent _)
    {
        lock (_metricSinkGate)
        {
            if (_metricSinkClosed)
            {
                return;
            }

            DistroConfigMetrics.SetMetricSink(metricName => HealthReporter.ReportSupportabilityCountMetric(metricName));
        }
    }

    // Shutdown and Dispose: stop listening for connect and detach the sink, for good (later records stay queued).
    private void CloseMetricSink()
    {
        lock (_metricSinkGate)
        {
            _metricSinkClosed = true;
            _agentConnectedSubscription?.Dispose();
            _agentConnectedSubscription = null;
            DistroConfigMetrics.SetMetricSink(null);
        }
    }

    public DistroXmlToggles CreateXmlToggles(DistroInstrumentationSites sites, DistroShippedXmlManifest manifest, DistroRegisterSites registerSites)
    {
        lock (_xmlTogglesGate)
        {
            if (_xmlTogglesClosed)
            {
                return null;
            }

            if (_xmlToggles != null)
            {
                return _xmlToggles;
            }

            try
            {
                // Constructing starts nothing (no file access, no watcher): ApplyAtStartup does that, outside this lock.
                var directories = DistroXmlFileSource.DirectoriesFor(ExtensionsDirectory);
                _xmlToggles = new DistroXmlToggles(
                    sites,
                    manifest,
                    new DistroXmlFileSource(directories),
                    new DistroXmlChangeWatcher(directories, DistroXmlChangeWatcher.ProductionDelayMilliseconds),
                    Configuration.DisableFileSystemWatcher,
                    registerSites,
                    ClearWrapperCaches);
                return _xmlToggles;
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Error("NR instrumentation XML will not be applied in distro mode: {0}", ex.GetType().Name + ": " + ex.Message);
                }
                catch (Exception)
                {
                }

                return null;
            }
        }
    }

    // After an XML change turned instrumentation off or on: re-arm wrappers WrapperService disabled after repeated
    // exceptions, as agent mode's InstrumentationWatcher does on a refresh. The toggles exist only after start set
    // _wrapperService, and they catch and log any failure.
    private void ClearWrapperCaches() => _wrapperService.ClearCaches();

    // Shutdown and Dispose: dispose the XML toggles (stops the watcher) and refuse to create another, for good.
    private void CloseXmlToggles()
    {
        DistroXmlToggles toggles;
        lock (_xmlTogglesGate)
        {
            _xmlTogglesClosed = true;
            toggles = _xmlToggles;
            _xmlToggles = null;
        }

        toggles?.Dispose();
    }

    public ITracer GetTracerImpl(string tracerFactoryName, uint tracerArguments, string metricName, string assemblyName, Type type, string typeName, string methodName, string argumentSignature, object invocationTarget, object[] arguments, ulong functionId, Type effectiveReturnType)
    {
        // Unreachable defensive guard: Start() completes (setting _wrapperService via
        // AfterWrapperServiceResolved) inside the constructor, before any caller can hold a reference
        // to this instance, so _wrapperService is never null here in practice. Kept as a cheap guard
        // against a future start sequence that resolves the wrapper service later.
        if (_wrapperService == null)
        {
            return null;
        }

        return WrapperTracerLookup.GetTracer(_wrapperService, tracerFactoryName, tracerArguments, metricName, type, methodName, argumentSignature, invocationTarget, arguments, functionId, effectiveReturnType);
    }

    // Distro mode never runs serverless: the OTel distro is a long-running-process front door.
    protected override bool ResolveServerlessMode(IBootstrapConfiguration c)
    {
        if (c.ServerlessModeEnabled)
        {
            Log.Warn("OTel distro mode: serverless mode is enabled in configuration, but distro mode does not support serverless. Continuing in non-serverless mode.");
        }

        return false;
    }

    // AgentApi.SetSupportabilityMetricCounters(...) is deliberately NOT called (no AfterAgentApiSet override):
    // touching the static AgentApi class runs its type initializer, which calls AgentInitializer.InitializeAgent()
    // and so constructs a second, full agent-mode AgentManager (and resets the logger). Nothing routes public API
    // calls to AgentApi without the profiler, so the counters would never be used in distro mode.
    protected override void StartModeServices()
    {
        Container.Resolve<CommandService>().AddCommands(
            new RestartCommand(),
            new ShutdownCommand()
        );

        ActivitySink = new DistroActivitySink(Container.Resolve<IAgent>(), Container.Resolve<IErrorService>());

        LogSink = new DistroLogSink(Container.Resolve<IAgent>());

        Metrics = new DistroMetricsSettings(Container.Resolve<IAgent>());

        // Core's logger is configured: resolution-fallback log lines held since before Core started are logged now.
        // Their counts (and every other distro metric) wait for connect: see OnAgentConnected.
        DistroMethodResolutionDiagnostics.SetLoggerReady(true);

        // The map already counted the wrappers it was built with; resolving IEnumerable<IWrapper> again would
        // re-run LoadWrappers() (the registration is a factory).
        LoadedWrapperCount = (Container.Resolve<IWrapperMap>() as WrapperMap)?.ExtensionWrapperCount ?? 0;
    }

    // Unlike AgentManager (one shot, swapped for DisabledAgentManager), DistroAgentHost allows a retry,
    // so release what this half-built instance registered before rethrowing.
    protected override void OnStartFailed()
    {
        Dispose();
    }

    public void Shutdown()
    {
        Shutdown(true);
    }

    protected override void Shutdown(bool cleanShutdown, bool closeLogging = true)
    {
        // First, and on every call: the plugin's ActivityListener outlives the agent (agent mode's listener is
        // disposed with the container), so stop it feeding Core before anything is torn down. Null when a
        // shutdown runs during start-up, before the sink exists.
        ActivitySink?.Disable();
        LogSink?.Disable();
        Metrics?.Disable();
        AgentShim.SetDistroAgentManager(null);
        DistroMethodResolutionDiagnostics.SetLoggerReady(false);
        CloseMetricSink();
        CloseXmlToggles();

        // The exit hook fires on both ProcessExit and DomainUnload, and a KillAgentEvent can race either.
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
        {
            return;
        }

        // Tear down the plugin's MeterProvider on every shutdown, not just a clean one: otherwise a non-clean
        // shutdown (KillAgentEvent, ForceDisconnect/410) would leave the plugin exporting with the license key
        // after Core is gone. A clean shutdown forces one final export before the final harvest (flush: true); a
        // non-clean one disposes without a final export (flush: false). DistroMetricsSettings is not subscribed to
        // PreCleanShutdownEvent for this: Metrics?.Disable() above (and the sink Disable()s next to it) runs before
        // base.Shutdown publishes that event, so this explicit call is ordered deterministically relative to the
        // disable; an event subscription would race it.
        Metrics?.Shutdown(flush: cleanShutdown);

        base.Shutdown(cleanShutdown, closeLogging);
    }

    public override void Dispose()
    {
        // Disposing the container without a shutdown must also stop the plugin's listener feeding Core.
        ActivitySink?.Disable();
        LogSink?.Disable();
        Metrics?.Disable();
        AgentShim.SetDistroAgentManager(null);
        DistroMethodResolutionDiagnostics.SetLoggerReady(false);
        CloseMetricSink();
        CloseXmlToggles();

        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // A bare Dispose (container torn down without a Shutdown) still has to stop the plugin's provider so it
        // cannot keep exporting once Core is gone; dispose without a final export. No-op if a prior Shutdown
        // already took the teardown callback.
        Metrics?.Shutdown(flush: false);

        UnsubscribeFromAgentEvents();
        base.Dispose();
    }
}
