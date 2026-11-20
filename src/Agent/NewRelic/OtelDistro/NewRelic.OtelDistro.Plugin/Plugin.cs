// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.OtelDistro;
using NewRelic.Agent.Core.Utilities;
using NewRelic.OtelDistro.Coordinator;
using OpenTelemetry.AutoInstrumentation.PluginApi;

namespace NewRelic.OtelDistro.Plugin;

/// <summary>
/// OpenTelemetry .NET auto-instrumentation plugin (OTEL_DOTNET_AUTO_PLUGINS) that starts New Relic Core
/// without the NR profiler and feeds it the application's Activities.
/// </summary>
public class Plugin : IPlugin
{
    private const string LogPrefix = "[NewRelic.OtelDistro.Plugin]";
    private const string NativeMethodsTypeName = "OpenTelemetry.AutoInstrumentation.NativeMethods";
    private const string RegisterExportName = "RegisterCallTargetDefinitions";

    private static readonly object StateLock = new object();

    // Keep-alive reference: prevents the listener from being garbage collected for the process lifetime.
    private static DistroActivityListener _listener;

    // Keep-alive reference for the EventBus subscription (the bus holds the delegate, this also lets us unsubscribe it).
    private static Action<AgentConnectedEvent> _connectedHandler;

    // Keep-alive reference for the Derived/Interface precedence check run on assembly loads.
    private static DistroOtelDerivedPrecedenceWatcher _precedenceWatcher;

    // Keep-alive reference for the EventBus subscription that detaches the watcher and drops the XML toggles on Core's
    // clean shutdown.
    private static Action<PreCleanShutdownEvent> _shutdownHandler;

    // The XML toggles Core created for the plugin's sites (GetAllDefinitionsPayload). Core owns and disposes them; the
    // plugin only drops this reference on Core's clean shutdown.
    private static DistroXmlToggles _xmlToggles;

    private static IDistroAgent _agent;
    private static bool _connected;
    private static bool _customEventRecorded;

    public void Initializing()
    {
        IDistroAgent distroAgent = null;
        var coreStartTimer = new Stopwatch();
        try
        {
            // Subscribe BEFORE starting: a synchronous connect (send-data-on-exit / sync startup) publishes
            // AgentConnectedEvent from inside Start(), and an async connect can finish before Start() returns.
            SubscribeToConnected();

            coreStartTimer.Start();
            distroAgent = DistroAgentHost.Start();
            coreStartTimer.Stop();
        }
        catch (Exception ex)
        {
            UnsubscribeFromConnected();

            // Core does not log a start-up failure in distro mode, so this is the only place it surfaces.
            Log($"Start-up failed: {ex}");
        }

        // NR's OTel env defaults (exporters none, conflicting OTel trace instrumentation off unless the customer set it),
        // applied whether or not Core started: OTel's exporters must never run, even when NR is not running. First thing
        // after the start attempt: OTel reads these settings only after Initializing() returns, and Core start reads none
        // of them. Never throws.
        ApplyOtelEnvironmentDefaults(coreStarted: distroAgent != null);

        if (distroAgent == null)
        {
            return;
        }

        try
        {
            Log(string.Format(CultureInfo.InvariantCulture, "Timing: core-start-ms={0:F1}", coreStartTimer.Elapsed.TotalMilliseconds));

            // The OTel ActivitySources of NR-instrumented libraries, excluded on top of the hybrid default list; must be set
            // before the listener attaches. A failed read of the generated table means no additions, never a failed start.
            distroAgent.ActivitySink.SetExcludedActivitySourceAdditions(ReadExcludedActivitySourceAdditions());

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
            Log($"Start-up failed: {ex}");
        }
    }

    // OTel calls this after it has built its settings, providers and definitions (Instrumentation.cs:229), so OTel's
    // effective configuration can be read here. Must never throw into OTel start-up.
    public void Initialized()
    {
        try
        {
            lock (StateLock)
            {
                if (_agent == null)
                {
                    // Core did not start: its logger and metric sink are not there to report to.
                    return;
                }
            }

            var timer = Stopwatch.StartNew();
            var snapshot = OtelConfigProbe.Probe(out var otelSites, out var failure);
            if (failure != null)
            {
                Log(snapshot == null ? $"OTel configuration detection unavailable: {failure}" : $"OTel configuration detection partly unavailable: {failure}");
            }

            DistroOtelConfigDiagnostics.Report(snapshot, ReadDisabledOtelTraceInstrumentationIds());

            var nrSites = OtelConfigProbe.ReadNrSites();
            DistroOtelPrecedenceDiagnostics.Report(otelSites, nrSites);
            StartDerivedPrecedenceWatcher(new DistroOtelDerivedPrecedenceWatcher(otelSites, nrSites));
            timer.Stop();

            Log(string.Format(CultureInfo.InvariantCulture, "Timing: otel-config-checks-ms={0:F1} otel-sites={1} nr-sites={2}", timer.Elapsed.TotalMilliseconds, otelSites.Count, nrSites.Count));
        }
        catch (Exception ex)
        {
            Log($"OTel configuration checks failed: {ex}");
        }
    }

    // OTel's Derived/Interface sites (e.g. ADONET on DbCommand) take every override in the derived types; whether an NR
    // target derives from one is only known once its assembly is loaded. Checked on each assembly load (and now, for the
    // assemblies already loaded), never on an instrumented call.
    private static void StartDerivedPrecedenceWatcher(DistroOtelDerivedPrecedenceWatcher watcher)
    {
        if (!watcher.HasWork)
        {
            return;
        }

        _precedenceWatcher = watcher;
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;

        // Detached on Core's clean shutdown (the same EventBus the connect subscription uses): after it there is no logger
        // or metric sink, and queued metrics would only grow. A non-clean shutdown leaves it attached (cheap, caught).
        SubscribeToPreCleanShutdown();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            watcher.OnAssemblyLoaded(assembly);
        }
    }

    // Once, for whichever of the precedence watcher and the XML toggles needs it first.
    private static void SubscribeToPreCleanShutdown()
    {
        Action<PreCleanShutdownEvent> handler;
        lock (StateLock)
        {
            if (_shutdownHandler != null)
            {
                return;
            }

            handler = OnPreCleanShutdown;
            _shutdownHandler = handler;
        }

        EventBus<PreCleanShutdownEvent>.Subscribe(handler);
    }

    private static void OnPreCleanShutdown(PreCleanShutdownEvent preCleanShutdownEvent)
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            _precedenceWatcher = null;
            _xmlToggles = null;
            Action<PreCleanShutdownEvent> handler;
            lock (StateLock)
            {
                handler = _shutdownHandler;
                _shutdownHandler = null;
            }

            if (handler != null)
            {
                EventBus<PreCleanShutdownEvent>.Unsubscribe(handler);
            }
        }
        catch (Exception)
        {
            // Never into Core's shutdown.
        }
    }

    private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
    {
        try
        {
            _precedenceWatcher?.OnAssemblyLoaded(args.LoadedAssembly);
        }
        catch (Exception)
        {
            // Never into an assembly load.
        }
    }

    // Non-public instance method the upstream PluginManager finds by name via reflection
    // (BindingFlags.NonPublic | BindingFlags.Instance) and whose result it treats as an
    // InstrumentationDefinitions.SharedPayload (PluginManager.cs). Returning the generated CallTarget
    // definitions here registers them with the OTel native profiler so the matched methods get wrapped.
    // Only the sites that are on are registered: the shipped defaults with the XML on disk applied (Core's XML toggles),
    // or the shipped defaults alone when Core is not running. They go through the process-wide registration coordinator
    // when there is one (each definition sent to OTel native once per process, so the payload returned is null); when the
    // coordinator is unavailable or fails, through today's payload. A site the XML turns on later is registered then,
    // through the router's run-time path (coordinator, or RegisterSites as the fallback).
    internal object GetAllDefinitionsPayload()
    {
        try
        {
            // definitions-payload-ms covers reading the generated definitions table, applying the XML on disk and
            // DistroDefinitionsPayload.Build. On the coordinator path it also covers coordinator creation (find or
            // create, which for the first AppDomain in the process includes the cross-domain setup) and the native
            // registration call; before the coordinator it covered only building the payload, so compare it across
            // that change with care. definitions-load-ms is the table read alone and xml-toggles-ms the XML step
            // alone. The read is DistroSiteTable's type initializer, reached through a non-inlined helper called inside the
            // timed window, so neither it nor the generated class's static initializer (which copies the table bytes) can
            // run before the Stopwatch starts.
            var payloadTimer = Stopwatch.StartNew();
            var loadTimer = Stopwatch.StartNew();
            var loaded = TryLoadGeneratedDefinitions(out var definitions, out var failureReason);
            loadTimer.Stop();
            if (!loaded)
            {
                // All or nothing: a defective table registers no NR definitions; OTel's own instrumentation carries on.
                Log($"Definitions table rejected: {failureReason}; no New Relic instrumentation registered.");
                return null;
            }

            var sites = DistroSiteTable.Sites;

            // Built before the toggles: its RegisterAtRunTime is the toggles' registerSites delegate.
            var coordinator = TryCreateCoordinatorRegistrar(definitions, out var coordinatorLocation, out var coordinatorUnavailableReason);
            var router = new DistroRegistrationRouter(coordinator, coordinatorLocation, coordinatorUnavailableReason, RegisterSites);

            var togglesTimer = Stopwatch.StartNew();
            DistroXmlToggles toggles = null;
            bool[] on = null;
            try
            {
                toggles = CreateXmlToggles(sites, DistroSiteTable.Manifest, router.RegisterAtRunTime);
                on = toggles?.ApplyAtStartup();
            }
            catch (Exception ex)
            {
                // Neither Core call throws; this covers the plugin's own part. The shipped defaults still register.
                toggles = null;
                on = null;
                Log($"NR instrumentation XML not applied; registering the shipped defaults: {ex.GetType().Name}: {ex.Message}");
            }

            togglesTimer.Stop();

            // From here the toggles count these sites as registered; when the payload cannot be built they are not.
            var onSites = OnSites(sites, on);
            try
            {
                if (router.RegisterAtStartup(onSites))
                {
                    // The coordinator registered them (or they already were, in this process): OTel's payload carries none.
                    payloadTimer.Stop();
                    LogPayloadTiming(payloadTimer, loadTimer, togglesTimer, 0, toggles);
                    return null;
                }

                SelectSites(definitions, sites, onSites, out var onDefinitions, out var functionIds);
                var payload = DistroDefinitionsPayload.Build(
                    onDefinitions,
                    typeof(Plugin).Assembly.FullName,
                    DistroPayloadId.For(functionIds));
                payloadTimer.Stop();
                LogPayloadTiming(payloadTimer, loadTimer, togglesTimer, onDefinitions.Count, toggles);
                return payload;
            }
            catch (Exception)
            {
                toggles?.MarkUnregistered(onSites);
                throw;
            }
        }
        catch (Exception ex)
        {
            Log($"Definitions payload failed: {ex}");
            return null;
        }
    }

    private static void LogPayloadTiming(Stopwatch payloadTimer, Stopwatch loadTimer, Stopwatch togglesTimer, int definitionCount, DistroXmlToggles toggles)
    {
        var xml = toggles?.StartupTimings;
        Log(string.Format(
            CultureInfo.InvariantCulture,
            "Timing: definitions-payload-ms={0:F1} definitions={1} definitions-load-ms={2:F1} xml-toggles-ms={3:F1} xml-list-ms={4:F1} xml-read-ms={5:F1} xml-compute-ms={6:F1} xml-report-ms={7:F1} xml-watcher-ms={8:F1}",
            payloadTimer.Elapsed.TotalMilliseconds,
            definitionCount,
            loadTimer.Elapsed.TotalMilliseconds,
            togglesTimer.Elapsed.TotalMilliseconds,
            xml?.ListMs ?? 0,
            xml?.ReadMs ?? 0,
            xml?.ComputeMs ?? 0,
            xml?.ReportMs ?? 0,
            xml?.WatcherMs ?? 0));
    }

    // The process-wide registration coordinator, adapted to Core's registrar seam, or null with the reason. Non-inlined and
    // guarded: only CoordinatorSiteRegistrar references the coordinator assembly, and that reference is first resolved
    // inside this try, so a missing or unloadable NewRelic.OtelDistro.Coordinator.dll gives null here, never a plugin
    // type-load failure. Never throws.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IDistroSiteRegistrar TryCreateCoordinatorRegistrar(IReadOnlyList<DistroCallTargetDefinition> definitions, out string location, out string unavailableReason)
    {
        try
        {
            var layoutProblem = CheckNativeDefinitionLayout();
            if (layoutProblem != null)
            {
                location = null;
                unavailableReason = WithOtelVersion(layoutProblem);
                Log($"Registration coordinator unavailable: {unavailableReason}");
                return null;
            }

            var registrar = CoordinatorSiteRegistrar.TryCreate(definitions, typeof(Plugin).Assembly.FullName, out location, out unavailableReason);
            Log(registrar != null
                ? $"Registration coordinator in use ({location}); coordinator assembly loaded from {CoordinatorSiteRegistrar.AssemblyLocation()}."
                : $"Registration coordinator unavailable: {unavailableReason}");
            return registrar;
        }
        catch (Exception ex)
        {
            location = null;
            unavailableReason = "coordinator assembly: " + ex.GetType().Name + ": " + ex.Message;
            Log($"Registration coordinator unavailable: {unavailableReason}");
            return null;
        }
    }

    // Null when the loaded OTel's native definition type and its RegisterCallTargetDefinitions export have the shapes the
    // coordinator's copies declare; otherwise why not. Non-inlined: it is the first to touch the coordinator assembly, so a
    // missing DLL throws when this is JIT-compiled, inside the caller's try, and lands in the same "coordinator assembly"
    // catch.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string CheckNativeDefinitionLayout()
    {
        var otelAssembly = typeof(OpenTelemetry.AutoInstrumentation.CallTarget.CallTargetState).Assembly;
        var otelType = otelAssembly.GetType(DistroDefinitionsPayload.NativeDefinitionTypeName);
        if (otelType == null)
        {
            return "native definition type " + DistroDefinitionsPayload.NativeDefinitionTypeName + " not found";
        }

        var expected = OtelNativeRegistrar.LayoutSignature;
        var found = DefinitionLayout.Describe(otelType);
        var difference = DefinitionLayout.FirstDifference(expected, found);
        if (difference != "none")
        {
            return "native definition layout differs from the coordinator's: " + difference;
        }

        var export = FindOtelRegisterExport(otelAssembly);
        if (export == null)
        {
            return "native registration export " + NativeMethodsTypeName + "." + RegisterExportName + " not found";
        }

        difference = DefinitionLayout.FirstDifference(OtelNativeRegistrar.RegisterSignature, DefinitionLayout.DescribeMethod(export));
        return difference == "none" ? null : "native registration export differs from the coordinator's: " + difference;
    }

    // OTel's NativeMethods.Windows.RegisterCallTargetDefinitions extern (non-public static), or null when it is not
    // exactly one method.
    private static MethodInfo FindOtelRegisterExport(Assembly otelAssembly)
    {
        var windows = otelAssembly.GetType(NativeMethodsTypeName)?.GetNestedType("Windows", BindingFlags.Public | BindingFlags.NonPublic);
        if (windows == null)
        {
            return null;
        }

        MethodInfo export = null;
        foreach (var method in windows.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (method.Name != RegisterExportName)
            {
                continue;
            }

            if (export != null)
            {
                return null;
            }

            export = method;
        }

        return export;
    }

    // Core's XML toggles for the plugin's sites, or null when Core is not running or could not create them (Core logs
    // that); the shipped defaults then stay in force for the process. Core owns the instance. An instance created here but
    // never applied (the caller's ApplyAtStartup not reached) is inert: OnChange is a no-op before ApplyAtStartup and its
    // watcher only starts there, so nothing reads the XML or calls registerSites.
    private static DistroXmlToggles CreateXmlToggles(DistroInstrumentationSites sites, DistroShippedXmlManifest manifest, DistroRegisterSites registerSites)
    {
        var agent = DistroAgentHost.Current;
        if (agent == null || sites == null || manifest == null)
        {
            return null;
        }

        var toggles = agent.CreateXmlToggles(sites, manifest, registerSites);
        if (toggles != null)
        {
            _xmlToggles = toggles;
            SubscribeToPreCleanShutdown();
        }

        return toggles;
    }

    // The indices of the sites to register at start-up: on[i] from the toggles, or each site's gate (the shipped
    // defaults) when there are no toggles.
    private static List<int> OnSites(DistroInstrumentationSites sites, bool[] on)
    {
        var count = on?.Length ?? sites.Count;
        var result = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            if (on != null ? on[i] : sites.IsOn(i))
            {
                result.Add(i);
            }
        }

        return result;
    }

    // Fallback path only: used when the process-wide registration coordinator is unavailable or fails (DistroRegistrationRouter);
    // with the coordinator, each definition is sent once per process under its NR-<pid>-<n> ids instead.
    // definitions[i] is site i. The function ids make the registration id (DistroPayloadId), so each different set of
    // sites gets its own id and OTel's native side does not ignore it as already processed. Start-up ids are content-only
    // (DistroPayloadId.For: deduped across AppDomains on purpose, one registration per process). Run-time ids add the
    // AppDomain id (ForAppDomain): each AppDomain runs its own plugin and reacts to the same XML edit, and OTel native
    // ignores a second registration under an id it has already processed (returns 0), which would turn the second
    // domain's gate off although OTel instruments the method process-wide. With per-domain ids native takes every
    // domain's registration; for a method it already instruments it only updates the existing entry (enabled categories)
    // and re-requests ReJIT from the original IL, so nothing is wrapped twice. Source: OTel v1.18.0 native
    // (OpenTelemetry.AutoInstrumentation.Native cor_profiler.cpp RegisterCallTargetDefinitions, rejit_handler.cpp).
    private static void SelectSites(IReadOnlyList<DistroCallTargetDefinition> definitions, DistroInstrumentationSites sites, IReadOnlyList<int> siteIndices, out List<DistroCallTargetDefinition> selected, out List<ulong> functionIds)
    {
        selected = new List<DistroCallTargetDefinition>(siteIndices.Count);
        functionIds = new List<ulong>(siteIndices.Count);
        foreach (var index in siteIndices)
        {
            if ((uint)index >= (uint)definitions.Count || (uint)index >= (uint)sites.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(siteIndices), index, "site index out of range (" + definitions.Count + " definitions)");
            }

            // FunctionId reads the site's record without building a site object (sites are built lazily, on first use).
            selected.Add(definitions[index]);
            functionIds.Add(sites.FunctionId(index));
        }
    }

    // Core's registerSites callback: registers the sites the XML on disk turns on after start-up. Runs on Core's watcher
    // thread, never under a lock, and blocks on native ReJIT. False makes Core keep the sites off and log the reason in the
    // NR log (prefixed with the OTel version here); the plugin's own log gets it too. Never throws.
    private static bool RegisterSites(IReadOnlyList<int> siteIndices, out string failure)
    {
        try
        {
            var definitions = DistroSiteTable.Definitions;
            var sites = DistroSiteTable.Sites;
            if (definitions == null || sites == null || siteIndices == null || siteIndices.Count == 0)
            {
                failure = WithOtelVersion("no definitions table or no sites to register");
                return false;
            }

            var timer = Stopwatch.StartNew();
            SelectSites(definitions, sites, siteIndices, out var selected, out var functionIds);
            var definitionsId = DistroPayloadId.ForAppDomain(functionIds, AppDomain.CurrentDomain.Id);
            if (!OtelRuntimeRegistration.TryRegister(selected, typeof(Plugin).Assembly.FullName, definitionsId, out var registered, out var reason))
            {
                failure = WithOtelVersion(reason);
                Log($"OTel run-time registration unavailable: {failure}");
                return false;
            }

            timer.Stop();
            Log(string.Format(CultureInfo.InvariantCulture, "Timing: runtime-registration-ms={0:F1} definitions={1} registered={2} id={3}", timer.Elapsed.TotalMilliseconds, selected.Count, registered, definitionsId));
            if (registered < selected.Count)
            {
                // Still success for Core (OTel took some of them); native skipped the rest as invalid.
                Log($"OTel run-time registration partial: OTel took {registered} of {selected.Count} definitions (id {definitionsId}).");
            }

            failure = null;
            return true;
        }
        catch (Exception ex)
        {
            failure = WithOtelVersion(ex.GetType().Name + ": " + ex.Message);
            Log($"OTel run-time registration unavailable: {failure}");
            return false;
        }
    }

    // "OTel <version>: <reason>", the version being the OpenTelemetry.AutoInstrumentation assembly's informational version
    // (e.g. "1.18.0", without a "+<commit>" suffix), then its assembly version, then "unknown". Never throws.
    private static string WithOtelVersion(string reason)
    {
        string version = null;
        try
        {
            var otelAssembly = typeof(OpenTelemetry.AutoInstrumentation.CallTarget.CallTargetState).Assembly;
            var informational = otelAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrEmpty(informational))
            {
                var plus = informational.IndexOf('+');
                version = plus >= 0 ? informational.Substring(0, plus) : informational;
            }

            if (string.IsNullOrEmpty(version))
            {
                version = otelAssembly.GetName().Version?.ToString();
            }
        }
        catch (Exception)
        {
            version = null;
        }

        return "OTel " + (string.IsNullOrEmpty(version) ? "unknown" : version) + ": " + reason;
    }

    // Isolated and non-inlined so that touching DistroSiteTable (and so its type initializer, which reads the table)
    // happens inside this method's call, i.e. inside the caller's timed window, never while the caller is JIT-compiled.
    // The read never throws; a defective table leaves Loaded false with a reason.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryLoadGeneratedDefinitions(out IReadOnlyList<DistroCallTargetDefinition> definitions, out string failureReason)
    {
        definitions = DistroSiteTable.Definitions;
        failureReason = DistroSiteTable.FailureReason;
        return DistroSiteTable.Loaded;
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

    // Core logs each applied/kept default itself. When Core did not start its logger may be the no-op one (or closed), so
    // the outcome goes to the plugin's own log as well. Must never throw into OTel start-up.
    private static void ApplyOtelEnvironmentDefaults(bool coreStarted)
    {
        try
        {
            var result = DistroOtelEnvironmentDefaults.Apply(new DistroProcessEnvironmentVariables(), ReadDisabledOtelTraceInstrumentationIds(), ReadNrCompilesNLogLogForwarding());
            if (!coreStarted)
            {
                Log(DistroOtelEnvironmentDefaults.FormatSummary(result));
            }
        }
        catch (Exception ex)
        {
            Log($"OTel environment defaults failed: {ex}");
        }
    }

    // Own try, so a generated-class initialization failure leaves an empty list: Apply must still force the exporters to none.
    // A separate non-inlined method, so the generated class's type initializer runs inside this try and not while
    // ApplyOtelEnvironmentDefaults is JIT-compiled.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IReadOnlyList<string> ReadDisabledOtelTraceInstrumentationIds()
    {
        try
        {
            return Generated.GeneratedDistroDefinitions.DisabledOtelTraceInstrumentationIds;
        }
        catch (Exception ex)
        {
            Log($"Generated OTel instrumentation IDs to disable unavailable; none will be disabled: {ex}");
            return new string[0];
        }
    }

    // Whether NR's NLog log-forwarding wrapper is compiled default-on, used only to diagnose a customer who also has
    // OTel's opt-in NLog bridge on (duplicate NLog lines). The generated value is a `const bool`, so this read is a
    // compile-time literal: it triggers no generated-type initializer, needs no try/NoInlining isolation, and the plugin
    // never writes the bridge setting. Build-time (compiled default-on), not the start-up gate state, on purpose:
    // Initializing() runs before GetAllDefinitionsPayload applies the on-disk XML toggles, and OTel reads the bridge
    // setting once anyway, so a later live NLog XML toggle could not change the warning's premise.
    private static bool ReadNrCompilesNLogLogForwarding()
    {
        return Generated.GeneratedDistroDefinitions.NrCompilesNLogLogForwarding;
    }

    // Same isolation as above: a failure means no distro-only ActivitySource exclusions, the hybrid default list still applies.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IReadOnlyList<string> ReadExcludedActivitySourceAdditions()
    {
        try
        {
            return Generated.GeneratedDistroDefinitions.ExcludedActivitySourceAdditions;
        }
        catch (Exception ex)
        {
            Log($"Generated ActivitySource exclusions unavailable; only the default exclude list applies: {ex}");
            return new string[0];
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
