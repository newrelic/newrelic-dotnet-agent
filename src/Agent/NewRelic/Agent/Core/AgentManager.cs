// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Threading;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Commands;
using NewRelic.Agent.Core.ContinuousProfiling;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Instrumentation;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.Tracer;
using NewRelic.Agent.Core.Wrapper;
using NewRelic.Agent.Extensions.Logging;
using Process = System.Diagnostics.Process;

namespace NewRelic.Agent.Core;

public sealed class AgentManager : AgentManagerBase, IAgentManager
{
    private static readonly IAgentManager DisabledAgentManager = new DisabledAgentManager();
    private static readonly AgentSingleton Singleton = new AgentSingleton();

    private sealed class AgentSingleton : Singleton<IAgentManager>
    {
        public AgentSingleton() : base(DisabledAgentManager) { }

        // Called by Singleton::Singleton
        protected override IAgentManager CreateInstance()
        {
            try
            {
                var agentManager = new AgentManager();

                //If the AgentManager received a shutdown event by the time the .ctor completes it means that the agent
                //was unable to start correctly. The shutdown process cannot switch the Singleton to use
                //the DisabledAgentManager because the Singleton is still being created so we need to do that here.
                if (agentManager.ShutdownEventReceived)
                {
                    agentManager.Shutdown(false);
                    return DisabledAgentManager;
                }

                return agentManager;
            }
            catch (Exception exception)
            {
                try
                {
                    // this log message may not get written if the exception happened during logger initialization
                    // or as a result of the ValidateAgentConfiguration() check
                    Log.Error(exception, "There was an error initializing the agent");
                }
                catch
                {
                    // ignored
                }

                return DisabledAgentManager;
            }
        }
    }

    public static IAgentManager Instance
    {
        get
        {
            // The singleton pointer may be null if we instrument a method that's invoked in the agent constructor.
            return Singleton?.ExistingInstance ?? DisabledAgentManager;
        }
    }

    private ThreadProfilingService _threadProfilingService;
    private ContinuousProfilingService _continuousProfilingService;

    // Set once, in AfterWrapperServiceResolved during start-up; read by GetTracerImpl as a plain field.
    private IWrapperService _wrapperService;

    protected override bool DistroModeEnabled => false;

    /// <summary>
    /// Creates an instance of the <see cref="AgentManager"/> class./>
    /// </summary>
    /// <remarks>
    /// The agent should be constructed as early as possible in order to perform
    /// initialization of the logging system.
    /// </remarks>
    private AgentManager() : base(new AgentManagerEnvironment())
    {
        Start();
    }

    protected override void AfterConfigurationPublished()
    {
        // delay agent startup to allow a debugger to be attached. Used primarily for local debugging of AWS Lambda functions
        if (BootstrapConfig.DebugStartupDelaySeconds > 0)
        {
            // writing directly to console, as Log output doesn't get flushed immediately. And, for some processes, even this doesn't write to the console.
            Console.WriteLine($"Delaying {BootstrapConfig.DebugStartupDelaySeconds} seconds. Attach debugger to {Process.GetCurrentProcess().MainModule?.FileName} now...");

            Thread.Sleep(BootstrapConfig.DebugStartupDelaySeconds * 1000);
            Debugger.Break(); // break the debugger, if one is attached
        }
    }

    protected override void AfterWrapperServiceResolved(IWrapperService wrapperService)
    {
        _wrapperService = wrapperService;

        if (Configuration.OpenTelemetryEnabled)
        {
            Container.Resolve<OpenTelemetryBridge.Tracing.ActivityBridge>().Start();

            if (!ServerlessModeEnabled)
            {
                // We need to resolve the MeterListenerBridge before the connect event is triggered so that
                // the MeterListenerBridge is ready to receive the connect event and start listening for
                // metrics.
                Container.Resolve<OpenTelemetryBridge.Metrics.MeterListenerBridge>().Start();
            }
        }

        // Must be constructed -- and therefore subscribed to AgentConnectedEvent -- BEFORE
        // AttemptAutoStart() below: ConnectionManager.Start() schedules the actual connect
        // asynchronously, so a fast connect can publish AgentConnectedEvent before a later-constructed
        // subscriber ever attaches, and EventBus does not replay missed events.
        //
        // Never construct in serverless mode: AttemptAutoStart() never runs there, so a constructed CP
        // would run full stop-the-world sampling sweeps every tick with every drain permanently
        // no-op'ing on !_isConnected -- pure overhead, no data ever shipped. This also forecloses CP's
        // agent commands in serverless (registered only when _continuousProfilingService != null).
        if (!ServerlessModeEnabled)
        {
            _continuousProfilingService = ContinuousProfilingServiceFactory.TryCreate(Container, Configuration, HealthReporter);
            _continuousProfilingService?.StartIfEnabled();
        }
    }

    protected override void AfterAgentApiSet()
    {
        Core.AgentApi.SetSupportabilityMetricCounters(Container.Resolve<IApiSupportabilityMetricCounters>());
    }

    protected override void StartModeServices()
    {
        var nativeMethods = Container.Resolve<INativeMethods>();
        var instrumentationService = Container.Resolve<IInstrumentationService>();

        _threadProfilingService = new ThreadProfilingService(Container.Resolve<IDataTransportService>(), nativeMethods, agentHealthReporter: Container.Resolve<IAgentHealthReporter>());

        // Mutually exclude the two profilers: the thread profiler refuses to start while continuous
        // profiling is active, and continuous profiling defers its start while a thread-profiling session
        // is in-flight. Wired here, post-construction (both were already constructed earlier in Start()),
        // rather than through constructors, since mutual constructor injection would be a cycle. A failed
        // CP construction leaves the field null, which both sides' guards already treat as "not running."
        _threadProfilingService.SetContinuousProfilingSessionControl(_continuousProfilingService);
        if (_continuousProfilingService != null)
            _continuousProfilingService.ThreadProfilingStatus = _threadProfilingService;

        if (!ServerlessModeEnabled)
        {
            var commandService = Container.Resolve<CommandService>();
            commandService.AddCommands(
                new RestartCommand(),
                new ShutdownCommand(),
                new StartThreadProfilerCommand(_threadProfilingService),
                new StopThreadProfilerCommand(_threadProfilingService),
                new InstrumentationUpdateCommand(instrumentationService)
            );

            // Null only if construction itself threw; the commands are simply not registered in that
            // case (CommandService.ProcessCommands handles an unknown command name gracefully).
            if (_continuousProfilingService != null)
            {
                var continuousProfilerResponseFormatter = new AckOnlyContinuousProfilerResponseFormatter();
                commandService.AddCommands(
                    new StartContinuousProfilerCommand(_continuousProfilingService, continuousProfilerResponseFormatter),
                    new StopContinuousProfilerCommand(_continuousProfilingService, continuousProfilerResponseFormatter)
                );
            }
        }

        Container.Resolve<InstrumentationWatcher>().Start();
        _threadProfilingService.Start();
    }

    protected override void OnStartFailed()
    {
        // AgentSingleton.CreateInstance's catch swaps in the DisabledAgentManager but never gets a
        // reference to this half-built manager, so it can't stop CP's native sampler if a later startup
        // step throws after CP has already been created/started. Before CP creation the field is still
        // null, so this is a no-op there.
        //
        // Only tear down CP here -- nothing else started by start-up has state that outlives the
        // exception (StartModeServices()'s own partial work is either idempotent-safe or immaterial once we
        // never become the live instance). CP's native sampler thread is the one piece that would
        // otherwise leak for the life of the process, since AgentSingleton.CreateInstance's catch never
        // gets a reference to this half-built manager.
        StopProfilerServices(null, () => _continuousProfilingService?.Dispose());
    }

    protected override void BeforeShutdown()
    {
        //Not every call to Shutdown will have access to the AgentSingleton, because some of the calls to Shutdown
        //will occur while the Singleton is being created. In those scenarios, the AgentSingleton will handle
        //Swapping out the AgentManager for the DisabledAgentManager.
        Singleton?.SetInstance(DisabledAgentManager);
    }

    protected override void StopModeServices()
    {
        StopServices();
    }

    private void StopServices()
    {
        StopProfilerServices(
            () => _threadProfilingService?.Stop(),
            () => _continuousProfilingService?.Dispose());
    }

    // Best-effort two-step profiler teardown. The continuous-profiling dispose MUST run even when stopping
    // thread profiling throws: ThreadProfilingService.Stop() joins a sampling worker and can throw, and the
    // original unguarded `Stop(); Dispose();` would then skip Dispose() and orphan the continuous-profiling
    // native sampler thread for the rest of the process (Shutdown()'s outer catch never compensates -- its
    // finally only disposes the container, not the CP service). The finally guarantees the CP dispose runs
    // while still letting a Stop() failure propagate so the shutdown path's error/health reporting is
    // preserved. Public static so the ordering/null-safety/isolation is unit-testable without constructing
    // AgentManager (whose ctor drives the full static startup path).
    public static void StopProfilerServices(Action stopThreadProfiler, Action disposeContinuousProfiler)
    {
        try
        {
            stopThreadProfiler?.Invoke();
        }
        finally
        {
            disposeContinuousProfiler?.Invoke();
        }
    }

    /// <summary>
    /// Returns a tracer.
    ///
    /// Implements the interface needed for IAgent.
    /// Called from AgentShim::GetTracer, which, in turn, are calls injected into profiled code.
    /// Consequently, this is called every time a profiled function gets called.
    /// </summary>
    /// <param name="tracerFactoryName">The fully qualified name of the TracerFactory,
    /// from the mapping held in the CoreInstrumentation.xml file,
    /// as set up by unmanaged the C++ profiler code.</param>
    /// <param name="tracerArguments">A packed value with items from the instrumentation .xml files</param>
    /// <param name="metricName"></param>
    /// <param name="assemblyName"></param>
    /// <param name="type"></param>
    /// <param name="typeName"></param>
    /// <param name="methodName"></param>
    /// <param name="argumentSignature"></param>
    /// <param name="invocationTarget"></param>
    /// <param name="arguments"></param>
    /// <param name="functionId"></param>
    /// <param name="effectiveReturnType">The type the instrumented body actually returns, for a
    /// runtime-async method; null for every other method and for a void effective return.</param>
    /// <returns>Returns an ITracer, although it is given as the much simpler Object;
    /// an Object is the preferred type because it has a trival type signature.</returns>
    public ITracer GetTracerImpl(string tracerFactoryName, uint tracerArguments, string metricName, string assemblyName, Type type, string typeName, string methodName, string argumentSignature, object invocationTarget, object[] arguments, ulong functionId, Type effectiveReturnType)
    {
        try
        {
            // First try to get a wrapper from the newer WrapperService
            var afterWrappedMethodDelegate = _wrapperService.BeforeWrappedMethod(type, methodName, argumentSignature, invocationTarget, arguments, tracerFactoryName, metricName, tracerArguments, functionId, effectiveReturnType);
            return (afterWrappedMethodDelegate != null) ? new WrapperTracer(afterWrappedMethodDelegate) : null;
        }
        catch (Exception e)
        {
            Log.Error(e, "Tracer invocation error");
            return null;
        }
    }
}
