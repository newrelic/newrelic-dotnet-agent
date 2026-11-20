// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Commands;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.DependencyInjection;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroAgentManagerTests
{
    private const string FatalConfigurationMessage = "There was a fatal configuration problem preventing the agent from starting. Please see previous log messages for details.";
    private const string ShutdownDuringStartMessage = "The agent received a shutdown event during start-up.";
    private const string ExtensionsDirectory = @"C:\nr\extensions";
    private const string PropagatorsMetric = "Supportability/Dotnet/OtelDistro/Config/PropagatorsNotDefault";
    private const string ExporterMetric = "Supportability/Dotnet/OtelDistro/Config/ExporterNotNone/Traces";
    private const string NotFoundMetric = "Supportability/Dotnet/OtelDistro/MethodResolution/NotFound";
    private const string AmbiguousMetric = "Supportability/Dotnet/OtelDistro/MethodResolution/Ambiguous";

    private List<string> _calls;
    private IAgentManagerEnvironment _environment;
    private IContainer _container;
    private IAgentHealthReporter _healthReporter;
    private IConfigurationService _configurationService;
    private IAgentApi _agentApi;
    private IWrapperService _wrapperService;
    private IConnectionManager _connectionManager;
    private IBootstrapConfiguration _bootstrapConfig;
    private CommandService _commandService;
    private WrapperMap _wrapperMap;
    private IAgent _agent;
    private IErrorService _errorService;
    private configuration _localConfig;
    private DistroAgentManager _manager;
    private bool _logInitialized;
    private IBootstrapConfiguration _originalBootstrapConfig;

    private Action<KillAgentEvent> _subscribedKillHandler;
    private EventHandler _subscribedExitHandler;
    private List<(bool Serverless, bool GcSamplerV2, bool Distro)> _registerFlags;
    private List<(bool Serverless, bool GcSamplerV2, bool Distro)> _startFlags;

    [SetUp]
    public void SetUp()
    {
        _calls = new List<string>();
        _registerFlags = new List<(bool, bool, bool)>();
        _startFlags = new List<(bool, bool, bool)>();
        _subscribedKillHandler = null;
        _subscribedExitHandler = null;
        _manager = null;
        _logInitialized = false;

        // DistroAgentManager.AdaptBootstrapConfig writes this static (ConfigurationService reads it directly);
        // save the real value so TearDown can restore it rather than clobbering whatever another fixture left.
        _originalBootstrapConfig = ConfigurationLoader.BootstrapConfig;

        _localConfig = new configuration();
        _bootstrapConfig = Mock.Create<IBootstrapConfiguration>();
        Mock.Arrange(() => _bootstrapConfig.LogConfig).Returns(Mock.Create<ILogConfig>());
        Mock.Arrange(() => _bootstrapConfig.ServerlessModeEnabled).Returns(false);
        Mock.Arrange(() => _bootstrapConfig.GCSamplerV2Enabled).Returns(true);

        _healthReporter = Mock.Create<IAgentHealthReporter>();
        Mock.Arrange(() => _healthReporter.ValidateAgentConfiguration()).Returns(true);

        _configurationService = Mock.Create<IConfigurationService>();
        _agentApi = Mock.Create<IAgentApi>();
        _wrapperService = Mock.Create<IWrapperService>();
        _connectionManager = Mock.Create<IConnectionManager>();
        Mock.Arrange(() => _connectionManager.AttemptAutoStart()).DoInstead(() => _calls.Add("AttemptAutoStart"));
        _commandService = new CommandService(Mock.Create<IDataTransportService>(), Mock.Create<IScheduler>(), Mock.Create<IConfigurationService>());
        _wrapperMap = new WrapperMap(new List<IWrapper> { Mock.Create<IWrapper>(), Mock.Create<IWrapper>(), Mock.Create<IWrapper>(), Mock.Create<IWrapper>() },
            Mock.Create<IDefaultWrapper>(), Mock.Create<INoOpWrapper>());
        _agent = Mock.Create<IAgent>();
        _errorService = Mock.Create<IErrorService>();

        _container = Mock.Create<IContainer>();
        Mock.Arrange(() => _container.Resolve<IConfigurationService>()).Returns(_configurationService);
        Mock.Arrange(() => _container.Resolve<IAgentHealthReporter>()).Returns(_healthReporter);
        Mock.Arrange(() => _container.Resolve<IAgentApi>()).Returns(_agentApi);
        Mock.Arrange(() => _container.Resolve<IWrapperService>()).Returns(_wrapperService);
        Mock.Arrange(() => _container.Resolve<IConnectionManager>()).Returns(_connectionManager);
        Mock.Arrange(() => _container.Resolve<CommandService>()).Returns(_commandService);
        Mock.Arrange(() => _container.Resolve<IWrapperMap>()).Returns(() => _wrapperMap);
        Mock.Arrange(() => _container.Resolve<IAgent>()).Returns(_agent);
        Mock.Arrange(() => _container.Resolve<IErrorService>()).Returns(_errorService);
        Mock.Arrange(() => _container.Dispose()).DoInstead(() => _calls.Add("ContainerDispose"));

        _environment = Mock.Create<IAgentManagerEnvironment>();
        Mock.Arrange(() => _environment.InitializeBootstrapLogger()).DoInstead(() => _calls.Add("InitializeBootstrapLogger"));
        Mock.Arrange(() => _environment.LoadConfiguration()).Returns(() => { _calls.Add("LoadConfiguration"); return (_localConfig, _bootstrapConfig); });
        Mock.Arrange(() => _environment.CreateContainer()).Returns(() => { _calls.Add("CreateContainer"); return _container; });
        Mock.Arrange(() => _environment.RegisterServices(Arg.IsAny<IContainer>(), Arg.AnyBool, Arg.AnyBool, Arg.AnyBool))
            .DoInstead((IContainer c, bool serverless, bool gcSamplerV2, bool distro) => _registerFlags.Add((serverless, gcSamplerV2, distro)));
        Mock.Arrange(() => _environment.StartServices(Arg.IsAny<IContainer>(), Arg.AnyBool, Arg.AnyBool, Arg.AnyBool))
            .DoInstead((IContainer c, bool serverless, bool gcSamplerV2, bool distro) => _startFlags.Add((serverless, gcSamplerV2, distro)));
        Mock.Arrange(() => _environment.SubscribeToKillAgent(Arg.IsAny<Action<KillAgentEvent>>()))
            .DoInstead((Action<KillAgentEvent> handler) => _subscribedKillHandler = handler);
        Mock.Arrange(() => _environment.UnsubscribeFromKillAgent(Arg.IsAny<Action<KillAgentEvent>>())).DoInstead(() => _calls.Add("UnsubscribeFromKillAgent"));
        Mock.Arrange(() => _environment.SubscribeToExit(Arg.IsAny<EventHandler>()))
            .DoInstead((EventHandler handler) => _subscribedExitHandler = handler);
        Mock.Arrange(() => _environment.UnsubscribeFromExit(Arg.IsAny<EventHandler>())).DoInstead(() => _calls.Add("UnsubscribeFromExit"));
        Mock.Arrange(() => _environment.ExtensionsDirectory).Returns(ExtensionsDirectory);
        Mock.Arrange(() => _environment.MarkAgentShuttingDown()).DoInstead(() => _calls.Add("MarkAgentShuttingDown"));
        Mock.Arrange(() => _environment.PublishCleanShutdownEvents()).DoInstead(() => _calls.Add("PublishCleanShutdownEvents"));
        Mock.Arrange(() => _environment.CloseLogging()).DoInstead(() => _calls.Add("CloseLogging"));
    }

    [TearDown]
    public void TearDown()
    {
        // Releases the configuration subscription the base created, for tests that never shut down.
        _manager?.Dispose();

        if (_logInitialized)
        {
            Log.Initialize(new NoOpLogger());
        }

        // Disposing these is a no-op (mock) or releases nothing that matters, but satisfies NUnit1032.
        _commandService?.Dispose();
        _container?.Dispose();
        _connectionManager?.Dispose();

        ConfigurationLoader.UseBootstrapConfigurationForTesting(_originalBootstrapConfig);

        AgentShim.ResetDistroStateForTesting();

        // Static sink: reset here (not at the end of the tests) so a failing assertion cannot leak it into other tests.
        DistroMethodResolutionDiagnostics.ResetForTesting();
        DistroConfigMetrics.ResetForTesting();
    }

    [Test]
    public void Start_RegistersServicesInDistroMode()
    {
        _manager = new DistroAgentManager(_environment);

        Assert.Multiple(() =>
        {
            Assert.That(_registerFlags, Is.EqualTo(new[] { (false, true, true) }));
            Assert.That(_startFlags, Is.EqualTo(new[] { (false, true, true) }));
            Assert.That(_manager.IsInitialized, Is.True);
            Assert.That(_calls, Does.Contain("AttemptAutoStart"));
        });
    }

    // F88: AdaptBootstrapConfig is the seam DistroAgentManager overrides so DefaultConfiguration's
    // EventListenerSamplersEnabled can see distro mode. ConfigurationService reads the static
    // ConfigurationLoader.BootstrapConfig directly rather than anything Start() returns, so the override must
    // land the wrapped instance there too.
    [Test]
    public void Start_WrapsBootstrapConfigSoDistroModeEnabledIsTrue()
    {
        _manager = new DistroAgentManager(_environment);

        Assert.Multiple(() =>
        {
            Assert.That(ConfigurationLoader.BootstrapConfig, Is.Not.SameAs(_bootstrapConfig));
            Assert.That(ConfigurationLoader.BootstrapConfig.DistroModeEnabled, Is.True);
            Assert.That(ConfigurationLoader.BootstrapConfig.ServerlessModeEnabled, Is.EqualTo(_bootstrapConfig.ServerlessModeEnabled));
        });
    }

    [Test]
    public void Start_ServerlessConfig_LogsWarningAndRunsNonServerless()
    {
        Mock.Arrange(() => _bootstrapConfig.ServerlessModeEnabled).Returns(true);
        var warnings = new List<string>();
        var logger = Mock.Create<ILogger>();
        Mock.Arrange(() => logger.Warn(Arg.IsAny<string>(), Arg.IsAny<object[]>())).DoInstead((string message, object[] args) => warnings.Add(message));
        Log.Initialize(logger);
        _logInitialized = true;

        _manager = new DistroAgentManager(_environment);

        Assert.Multiple(() =>
        {
            Assert.That(warnings, Has.Some.Contains("serverless mode is enabled in configuration, but distro mode does not support serverless"));
            Assert.That(_registerFlags, Is.EqualTo(new[] { (false, true, true) }));
            Assert.That(_startFlags, Is.EqualTo(new[] { (false, true, true) }));
            Assert.That(_calls, Does.Contain("AttemptAutoStart"));
        });
        Mock.Assert(() => _connectionManager.AttemptAutoStart(), Occurs.Once());
    }

    [Test]
    public void Start_CallsInitializeBootstrapLoggerFirst()
    {
        _manager = new DistroAgentManager(_environment);

        Assert.That(_calls, Has.Count.GreaterThan(1));
        Assert.Multiple(() =>
        {
            Assert.That(_calls[0], Is.EqualTo("InitializeBootstrapLogger"));
            Assert.That(_calls[1], Is.EqualTo("LoadConfiguration"));
            Assert.That(_calls.FindAll(c => c == "InitializeBootstrapLogger"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Start_CreatesActivitySinkAndCountsExtensionWrappers()
    {
        _manager = new DistroAgentManager(_environment);

        Assert.Multiple(() =>
        {
            Assert.That(_manager.ActivitySink, Is.Not.Null);
            Assert.That(_manager.ActivitySink.IsDisabled, Is.False);
            Assert.That(_manager.LoadedWrapperCount, Is.EqualTo(4));
        });
        Mock.Assert(() => _container.Resolve<IEnumerable<IWrapper>>(), Occurs.Never());
        // ActivitySink, LogSink, and Metrics (DistroMetricsSettings) each resolve IAgent once.
        Mock.Assert(() => _container.Resolve<IAgent>(), Occurs.Exactly(3));
        Mock.Assert(() => _container.Resolve<IErrorService>(), Occurs.Once());
        Mock.Assert(() => _container.Resolve<CommandService>(), Occurs.Once());
    }

    // Finding 1 (final review): Core's metric aggregator drops what is recorded before connect, so the distro metric
    // sink is only attached on AgentConnectedEvent. The mocked AttemptAutoStart publishes nothing: an async connect.
    [Test]
    public void Start_AsyncConnect_HoldsMetricsUntilAgentConnected_ThenSendsThemOnce()
    {
        var site = new DistroInstrumentationSite("Factory", "Metric", 0u, "Asm", "NS.Worker", "DoWork", string.Empty, 1ul);
        DistroConfigMetrics.Record(PropagatorsMetric);
        DistroMethodResolutionDiagnostics.Report(new DistroResolvedMethod(DistroResolutionOutcome.NotFound, false, 1ul, typeof(object), string.Empty), site, 1);

        _manager = new DistroAgentManager(_environment);
        DistroConfigMetrics.Record(ExporterMetric);
        Mock.Assert(() => _healthReporter.ReportSupportabilityCountMetric(Arg.AnyString, Arg.AnyLong), Occurs.Never());

        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

        Mock.Assert(() => _healthReporter.ReportSupportabilityCountMetric(PropagatorsMetric, 1), Occurs.Once());
        Mock.Assert(() => _healthReporter.ReportSupportabilityCountMetric(NotFoundMetric, 1), Occurs.Once());
        Mock.Assert(() => _healthReporter.ReportSupportabilityCountMetric(ExporterMetric, 1), Occurs.Once());

        // Attached: later records go straight to the reporter.
        DistroMethodResolutionDiagnostics.Report(new DistroResolvedMethod(DistroResolutionOutcome.AmbiguousDisagreeing, false, 1ul, typeof(object), string.Empty), site, 1);
        Mock.Assert(() => _healthReporter.ReportSupportabilityCountMetric(AmbiguousMetric, 1), Occurs.Once());

        // A reconnect re-attaches harmlessly: nothing is sent twice.
        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());
        Mock.Assert(() => _healthReporter.ReportSupportabilityCountMetric(Arg.AnyString, Arg.AnyLong), Occurs.Exactly(4));
    }

    // A synchronous connect publishes AgentConnectedEvent inside start-up step 11; the subscription (step 10) catches it.
    [Test]
    public void Start_SyncConnect_AttachesSinkDuringStart()
    {
        Mock.Arrange(() => _connectionManager.AttemptAutoStart()).DoInstead(() => EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent()));
        DistroConfigMetrics.Record(PropagatorsMetric);

        _manager = new DistroAgentManager(_environment);

        Mock.Assert(() => _healthReporter.ReportSupportabilityCountMetric(PropagatorsMetric, 1), Occurs.Once());
        DistroConfigMetrics.Record(ExporterMetric);
        Mock.Assert(() => _healthReporter.ReportSupportabilityCountMetric(ExporterMetric, 1), Occurs.Once());
    }

    [Test]
    public void Shutdown_DetachesSink_AndALaterConnectDoesNotReattach()
    {
        _manager = new DistroAgentManager(_environment);
        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());
        DistroConfigMetrics.Record(PropagatorsMetric);
        Mock.Assert(() => _healthReporter.ReportSupportabilityCountMetric(PropagatorsMetric, 1), Occurs.Once());

        _manager.Shutdown();
        DistroConfigMetrics.Record(PropagatorsMetric);
        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

        Mock.Assert(() => _healthReporter.ReportSupportabilityCountMetric(PropagatorsMetric, 1), Occurs.Once());

        // Still queued, not lost: a later sink (a new agent) gets it.
        var sent = new List<string>();
        DistroConfigMetrics.SetMetricSink(sent.Add);
        Assert.That(sent, Is.EqualTo(new[] { PropagatorsMetric }));
    }

    [Test]
    public void Dispose_ALaterConnectDoesNotReattach()
    {
        _manager = new DistroAgentManager(_environment);
        _manager.Dispose();

        DistroConfigMetrics.Record(PropagatorsMetric);
        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

        Mock.Assert(() => _healthReporter.ReportSupportabilityCountMetric(Arg.AnyString, Arg.AnyLong), Occurs.Never());
    }

    // The connect handler re-checks under the gate: a connect whose handler was already captured by EventBus.Publish's
    // copied invocation list, but runs after shutdown closed the gate, does not re-attach.
    [Test]
    public void ConnectHandlerRunningAfterShutdown_DoesNotReattach()
    {
        // Subscribed before the manager, so it runs first in the same Publish and shuts the manager down.
        Action<AgentConnectedEvent> shutdownFirst = _ => _manager?.Shutdown();
        EventBus<AgentConnectedEvent>.Subscribe(shutdownFirst);
        try
        {
            _manager = new DistroAgentManager(_environment);
            DistroConfigMetrics.Record(PropagatorsMetric);
            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());
        }
        finally
        {
            EventBus<AgentConnectedEvent>.Unsubscribe(shutdownFirst);
        }

        Mock.Assert(() => _healthReporter.ReportSupportabilityCountMetric(Arg.AnyString, Arg.AnyLong), Occurs.Never());
    }

    // Finding 1d: the held resolution log lines go out when Core's logger is ready (Core start), not at connect.
    [Test]
    public void Start_FlushesHeldResolutionLogLinesAtCoreStart_BeforeConnect()
    {
        var infos = CaptureInfoLines();
        var site = new DistroInstrumentationSite("Factory", "Metric", 0u, "Asm", "NS.Worker", "DoWork", string.Empty, 1ul);
        DistroMethodResolutionDiagnostics.Report(new DistroResolvedMethod(DistroResolutionOutcome.NotFound, false, 1ul, typeof(object), string.Empty), site, 1);
        Assert.That(infos.Where(m => m.Contains("could not identify the method")), Is.Empty);

        _manager = new DistroAgentManager(_environment);

        Assert.That(infos.Where(m => m.Contains("could not identify the method")).Count(), Is.EqualTo(1));
        Mock.Assert(() => _healthReporter.ReportSupportabilityCountMetric(Arg.AnyString, Arg.AnyLong), Occurs.Never());
    }

    [Test]
    public void Shutdown_HoldsResolutionLogLinesAgain()
    {
        var infos = CaptureInfoLines();
        _manager = new DistroAgentManager(_environment);
        _manager.Shutdown();

        DistroMethodResolutionDiagnostics.Report(new DistroResolvedMethod(DistroResolutionOutcome.NotFound, false, 1ul, typeof(object), string.Empty), new DistroInstrumentationSite("Factory", "Metric", 0u, "Asm", "NS.Worker", "DoWork", string.Empty, 1ul), 1);

        Assert.That(infos.Where(m => m.Contains("could not identify the method")), Is.Empty);
    }

    // Core's own start-up lines are not all format strings, so the raw template and its arguments are captured.
    private List<string> CaptureInfoLines()
    {
        var infos = new List<string>();
        var logger = Mock.Create<ILogger>();
        Mock.Arrange(() => logger.Info(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
            .DoInstead((string message, object[] args) => infos.Add(message + " | " + string.Join(", ", args ?? new object[0])));
        Log.Initialize(logger);
        _logInitialized = true;
        return infos;
    }

    [Test]
    public void Start_CreatesLogSink()
    {
        _manager = new DistroAgentManager(_environment);

        Assert.Multiple(() =>
        {
            Assert.That(_manager.LogSink, Is.Not.Null);
            Assert.That(_manager.LogSink.IsDisabled, Is.False);
        });
    }

    [Test]
    public void Start_WrapperMapIsNotTheConcreteType_CountsZero()
    {
        _wrapperMap = null;
        Mock.Arrange(() => _container.Resolve<IWrapperMap>()).Returns(Mock.Create<IWrapperMap>());

        _manager = new DistroAgentManager(_environment);

        Assert.That(_manager.LoadedWrapperCount, Is.EqualTo(0));
    }

    [Test]
    public void Start_ExposesAgentStateThroughIDistroAgent()
    {
        _manager = new DistroAgentManager(_environment);
        IDistroAgent distroAgent = _manager;

        Assert.Multiple(() =>
        {
            Assert.That(distroAgent.AgentApi, Is.SameAs(_agentApi));
            Assert.That(distroAgent.ExtensionsDirectory, Is.EqualTo(ExtensionsDirectory));
            Assert.That(distroAgent.Configuration, Is.Not.Null);
            Assert.That(distroAgent.ActivitySink, Is.SameAs(_manager.ActivitySink));
            Assert.That(distroAgent.LogSink, Is.SameAs(_manager.LogSink));
            Assert.That(distroAgent.LoadedWrapperCount, Is.EqualTo(_manager.LoadedWrapperCount));
        });
    }

    [Test]
    public void Start_WhenConfigurationInvalid_DisposesContainer()
    {
        Mock.Arrange(() => _healthReporter.ValidateAgentConfiguration()).Returns(false);

        var thrown = Assert.Throws<Exception>(() => _manager = new DistroAgentManager(_environment));

        Assert.Multiple(() =>
        {
            Assert.That(thrown.Message, Is.EqualTo(FatalConfigurationMessage));
            Assert.That(_calls.FindAll(c => c == "ContainerDispose"), Has.Count.EqualTo(1));
            Assert.That(_calls.FindAll(c => c == "MarkAgentShuttingDown"), Has.Count.EqualTo(1));
            Assert.That(_calls, Does.Not.Contain("PublishCleanShutdownEvents"));
        });
    }

    [Test]
    public void Start_WhenStepThrows_DisposesAndRethrows()
    {
        var expected = new InvalidOperationException("connect failed");
        Mock.Arrange(() => _container.Resolve<IConnectionManager>()).Throws(expected);

        var thrown = Assert.Throws<InvalidOperationException>(() => _manager = new DistroAgentManager(_environment));

        Assert.Multiple(() =>
        {
            Assert.That(thrown, Is.SameAs(expected));
            Assert.That(_calls.FindAll(c => c == "ContainerDispose"), Has.Count.EqualTo(1));
            Assert.That(_calls, Does.Contain("UnsubscribeFromKillAgent"));
            Assert.That(_calls, Does.Not.Contain("MarkAgentShuttingDown"));
        });
    }

    [Test]
    public void Start_WhenKillAgentDuringStart_ShutsDownAndThrows()
    {
        Mock.Arrange(() => _environment.InitializeExtensions(Arg.AnyString)).DoInstead(() => _subscribedKillHandler(new KillAgentEvent()));

        var thrown = Assert.Throws<Exception>(() => _manager = new DistroAgentManager(_environment));

        Assert.Multiple(() =>
        {
            Assert.That(thrown.Message, Is.EqualTo(ShutdownDuringStartMessage));
            Assert.That(_calls.FindAll(c => c == "MarkAgentShuttingDown"), Has.Count.EqualTo(1));
            Assert.That(_calls.FindAll(c => c == "ContainerDispose"), Has.Count.EqualTo(1));
            Assert.That(_calls, Does.Not.Contain("PublishCleanShutdownEvents"));
        });
    }

    // DistroActivitySink.Disable() is a one-way idempotent latch, so a second Disable() call is not observable through
    // the public surface; this asserts only that the shutdown steps run once and the sink ends up disabled.
    [Test]
    public void Shutdown_CalledTwice_RunsShutdownStepsOnceAndLeavesSinkDisabled()
    {
        _manager = new DistroAgentManager(_environment);
        _calls.Clear();

        _manager.Shutdown();
        _manager.Shutdown();

        Assert.Multiple(() =>
        {
            Assert.That(_calls.FindAll(c => c == "MarkAgentShuttingDown"), Has.Count.EqualTo(1));
            Assert.That(_calls.FindAll(c => c == "PublishCleanShutdownEvents"), Has.Count.EqualTo(1));
            Assert.That(_calls.FindAll(c => c == "ContainerDispose"), Has.Count.EqualTo(1));
            Assert.That(_calls.FindAll(c => c == "CloseLogging"), Has.Count.EqualTo(1));
            Assert.That(_manager.ActivitySink.IsDisabled, Is.True);
        });
    }

    [Test]
    public void Shutdown_DisablesLogSink()
    {
        _manager = new DistroAgentManager(_environment);

        _manager.Shutdown();

        Assert.That(_manager.LogSink.IsDisabled, Is.True);
    }

    [Test]
    public void ExitHook_ShutsDownCleanOnce()
    {
        _manager = new DistroAgentManager(_environment);
        _calls.Clear();

        _subscribedExitHandler(this, EventArgs.Empty);
        _subscribedExitHandler(this, EventArgs.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(_calls.FindAll(c => c == "MarkAgentShuttingDown"), Has.Count.EqualTo(1));
            Assert.That(_calls.FindAll(c => c == "PublishCleanShutdownEvents"), Has.Count.EqualTo(1));
            Assert.That(_manager.ActivitySink.IsDisabled, Is.True);
        });
    }

    [Test]
    public void KillAgentEvent_AfterStart_ShutsDownNotClean()
    {
        _manager = new DistroAgentManager(_environment);
        _calls.Clear();

        _subscribedKillHandler(new KillAgentEvent());

        Assert.Multiple(() =>
        {
            Assert.That(_calls.FindAll(c => c == "MarkAgentShuttingDown"), Has.Count.EqualTo(1));
            Assert.That(_calls, Does.Not.Contain("PublishCleanShutdownEvents"));
            Assert.That(_calls.FindAll(c => c == "ContainerDispose"), Has.Count.EqualTo(1));
            Assert.That(_manager.ActivitySink.IsDisabled, Is.True);
        });
    }

    [Test]
    public void Dispose_DisablesLogSink()
    {
        _manager = new DistroAgentManager(_environment);

        _manager.Dispose();

        Assert.That(_manager.LogSink.IsDisabled, Is.True);
    }

    [Test]
    public void Dispose_CalledTwice_DisposesContainerOnce()
    {
        _manager = new DistroAgentManager(_environment);
        _calls.Clear();

        _manager.Dispose();
        _manager.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(_calls.FindAll(c => c == "ContainerDispose"), Has.Count.EqualTo(1));
            Assert.That(_calls.FindAll(c => c == "UnsubscribeFromKillAgent"), Has.Count.EqualTo(1));
            Assert.That(_calls.FindAll(c => c == "UnsubscribeFromExit"), Has.Count.EqualTo(1));
            Assert.That(_calls, Does.Not.Contain("MarkAgentShuttingDown"));
            Assert.That(_manager.ActivitySink.IsDisabled, Is.True);
            Assert.That(_manager.LogSink.IsDisabled, Is.True);
        });
    }

    [Test]
    public void Start_CreatesMetricsSettings()
    {
        _manager = new DistroAgentManager(_environment);

        Assert.Multiple(() =>
        {
            Assert.That(_manager.Metrics, Is.Not.Null);
            Assert.That(_manager.Metrics.IsDisabled, Is.False);
            IDistroAgent distroAgent = _manager;
            Assert.That(distroAgent.Metrics, Is.SameAs(_manager.Metrics));
        });
    }

    // Disable() runs first (stops the event feed), then the teardown callback fires with flush: true so the plugin
    // forces one final export before the final harvest.
    [Test]
    public void Shutdown_DisablesThenTearsDownMetricsWithFlush()
    {
        _manager = new DistroAgentManager(_environment);
        var teardownCount = 0;
        bool? capturedFlush = null;
        _manager.Metrics.RegisterShutdown(flush => { teardownCount++; capturedFlush = flush; });

        _manager.Shutdown();

        Assert.Multiple(() =>
        {
            Assert.That(teardownCount, Is.EqualTo(1));
            Assert.That(capturedFlush, Is.True);
            Assert.That(_manager.Metrics.IsDisabled, Is.True);
        });
    }

    // A non-clean shutdown (KillAgentEvent after start) must still tear the plugin's provider down, but with
    // flush: false so no final export ships after Core is gone.
    [Test]
    public void KillAgentEvent_TearsDownMetricsWithoutFlush()
    {
        _manager = new DistroAgentManager(_environment);
        var teardownCount = 0;
        bool? capturedFlush = null;
        _manager.Metrics.RegisterShutdown(flush => { teardownCount++; capturedFlush = flush; });

        _subscribedKillHandler(new KillAgentEvent());

        Assert.Multiple(() =>
        {
            Assert.That(teardownCount, Is.EqualTo(1));
            Assert.That(capturedFlush, Is.False);
            Assert.That(_manager.Metrics.IsDisabled, Is.True);
        });
    }

    // A bare Dispose (no Shutdown) still tears the provider down, with flush: false.
    [Test]
    public void Dispose_TearsDownMetricsWithoutFlush()
    {
        _manager = new DistroAgentManager(_environment);
        var teardownCount = 0;
        bool? capturedFlush = null;
        _manager.Metrics.RegisterShutdown(flush => { teardownCount++; capturedFlush = flush; });

        _manager.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(teardownCount, Is.EqualTo(1));
            Assert.That(capturedFlush, Is.False);
            Assert.That(_manager.Metrics.IsDisabled, Is.True);
        });
    }

    // AdaptBootstrapConfig returns a null bootstrap config straight through without wrapping it, so the static is
    // never replaced with a DistroBootstrapConfiguration. Start then fails downstream on the null config.
    [Test]
    public void Start_NullBootstrapConfig_DoesNotWrapAndThrows()
    {
        Mock.Arrange(() => _environment.LoadConfiguration()).Returns(() => { _calls.Add("LoadConfiguration"); return (_localConfig, (IBootstrapConfiguration)null); });

        Assert.Throws<NullReferenceException>(() => _manager = new DistroAgentManager(_environment));

        // The null guard returned without touching the static, so it is still the value SetUp saved.
        Assert.That(ConfigurationLoader.BootstrapConfig, Is.SameAs(_originalBootstrapConfig));
    }

    [Test]
    public void GetTracerImpl_AfterStart_UsesResolvedWrapperService()
    {
        _manager = new DistroAgentManager(_environment);
        IAgentManager agentManager = _manager;

        var type = typeof(string);
        var invocationTarget = new object();
        var arguments = new object[] { 1, 2 };
        AfterWrappedMethodDelegate afterWrappedMethodDelegate = (_, __) => { };

        Mock.Arrange(() => _wrapperService.BeforeWrappedMethod(type, "M", "System.Int32", invocationTarget, arguments, "Factory", "Metric", 7u, 42ul, null))
            .Returns(afterWrappedMethodDelegate);

        var result = agentManager.GetTracerImpl("Factory", 7u, "Metric", "AssemblyName", type, "TypeName", "M", "System.Int32", invocationTarget, arguments, 42ul, null);

        Assert.That(result, Is.InstanceOf<WrapperTracer>());
        Mock.Assert(() => _wrapperService.BeforeWrappedMethod(type, "M", "System.Int32", invocationTarget, arguments, "Factory", "Metric", 7u, 42ul, null), Occurs.Once());
    }

    // Covers the lookup's null-delegate path (BeforeWrappedMethod returns null), not the unreachable
    // _wrapperService == null guard in GetTracerImpl.
    [Test]
    public void GetTracerImpl_BeforeWrappedMethodReturnsNull_ReturnsNull()
    {
        _manager = new DistroAgentManager(_environment);
        IAgentManager agentManager = _manager;

        Mock.Arrange(() => _wrapperService.BeforeWrappedMethod(Arg.IsAny<Type>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<object>(), Arg.IsAny<object[]>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<uint>(), Arg.IsAny<ulong>(), Arg.IsAny<Type>()))
            .Returns((AfterWrappedMethodDelegate)null);

        var result = agentManager.GetTracerImpl("Factory", 7u, "Metric", "AssemblyName", typeof(string), "TypeName", "M", "System.Int32", new object(), new object[0], 42ul, null);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Shutdown_UnregistersFromShim()
    {
        AgentShim.EnterDistroMode();
        _manager = new DistroAgentManager(_environment);
        AgentShim.SetDistroAgentManager(_manager);

        _manager.Shutdown();

        var tracer = AgentShim.GetTracer("Factory", 7u, "Metric", "AssemblyName", typeof(string), "TypeName", "M", "System.Int32", new object(), new object[0], 42ul, null);

        Assert.That(tracer, Is.Null);
        Mock.Assert(() => _wrapperService.BeforeWrappedMethod(Arg.IsAny<Type>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<object>(), Arg.IsAny<object[]>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<uint>(), Arg.IsAny<ulong>(), Arg.IsAny<Type>()), Occurs.Never());
    }

    [Test]
    public void Dispose_UnregistersFromShim()
    {
        AgentShim.EnterDistroMode();
        _manager = new DistroAgentManager(_environment);
        AgentShim.SetDistroAgentManager(_manager);

        _manager.Dispose();

        var tracer = AgentShim.GetTracer("Factory", 7u, "Metric", "AssemblyName", typeof(string), "TypeName", "M", "System.Int32", new object(), new object[0], 42ul, null);

        Assert.That(tracer, Is.Null);
        Mock.Assert(() => _wrapperService.BeforeWrappedMethod(Arg.IsAny<Type>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<object>(), Arg.IsAny<object[]>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<uint>(), Arg.IsAny<ulong>(), Arg.IsAny<Type>()), Occurs.Never());
    }

    // ---- XML toggles

    private const string ShippedXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<extension xmlns=\"urn:newrelic-extension\">\n<instrumentation>\n"
        + "  <tracerFactory name=\"F1\">\n    <match assemblyName=\"Asm\" className=\"NS.Foo\">\n      <exactMethodMatcher methodName=\"Run\" parameters=\"\" />\n    </match>\n  </tracerFactory>\n"
        + "</instrumentation>\n</extension>\n";

    private static DistroShippedXmlTestData.Shipped ShippedTable() => new DistroShippedXmlTestData.Shipped(new[] { ("A.xml", ShippedXml, true) });

    private static bool RegisterNothing(IReadOnlyList<int> indices, out string failure)
    {
        failure = null;
        return true;
    }

    // Records messages by level. Some Core start-up lines use named placeholders ("{Version}"), which string.Format rejects,
    // so a message that does not format is kept as written.
    private sealed class FormattingTolerantLog
    {
        private readonly List<(Level Level, string Message)> _entries = new List<(Level, string)>();

        public void Add(Level level, string message, object[] args)
        {
            string text;
            try
            {
                text = args == null || args.Length == 0 ? message : string.Format(message, args);
            }
            catch (FormatException)
            {
                text = message;
            }

            lock (_entries)
            {
                _entries.Add((level, text));
            }
        }

        public string[] Messages(Level level)
        {
            lock (_entries)
            {
                return _entries.Where(e => e.Level == level).Select(e => e.Message).ToArray();
            }
        }
    }

    private FormattingTolerantLog RecordingLogger()
    {
        var log = new FormattingTolerantLog();
        var logger = Mock.Create<ILogger>();
        Mock.Arrange(() => logger.Debug(Arg.AnyString, Arg.IsAny<object[]>())).DoInstead((string m, object[] a) => log.Add(Level.Debug, m, a));
        Mock.Arrange(() => logger.Info(Arg.AnyString, Arg.IsAny<object[]>())).DoInstead((string m, object[] a) => log.Add(Level.Info, m, a));
        Mock.Arrange(() => logger.Warn(Arg.AnyString, Arg.IsAny<object[]>())).DoInstead((string m, object[] a) => log.Add(Level.Warn, m, a));
        Mock.Arrange(() => logger.Error(Arg.AnyString, Arg.IsAny<object[]>())).DoInstead((string m, object[] a) => log.Add(Level.Error, m, a));
        Log.Initialize(logger);
        _logInitialized = true;
        return log;
    }

    [Test]
    public void CreateXmlToggles_ReadsTheExtensionsDirectoryAndItsRuntimeSubfolder()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nr-distro-manager-xml-" + Guid.NewGuid().ToString("N"));
        var runtime = System.IO.Path.Combine(root, AgentInstallConfiguration.RuntimeDirectoryName);
        System.IO.Directory.CreateDirectory(runtime);
        try
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(root, "A.xml"), ShippedXml);
            System.IO.File.WriteAllText(System.IO.Path.Combine(runtime, "Custom.xml"), ShippedXml.Replace("\"Run\"", "\"Custom\""));
            Mock.Arrange(() => _environment.ExtensionsDirectory).Returns(root);
            var logger = RecordingLogger();
            _manager = new DistroAgentManager(_environment);
            var shipped = ShippedTable();

            var toggles = ((IDistroAgent)_manager).CreateXmlToggles(shipped.Sites, shipped.Manifest, RegisterNothing);
            var registerNow = toggles.ApplyAtStartup();

            Assert.Multiple(() =>
            {
                Assert.That(registerNow, Is.EqualTo(new[] { true, true }), "plugin site and the unchanged shipped Run");
                Assert.That(logger.Messages(Level.Warn), Has.Some.Contains("Custom.xml names NS.Foo.Custom"));
                Assert.That(logger.Messages(Level.Debug), Has.None.Contains("disabled by local configuration"), "the default configuration watches");
                Assert.That(logger.Messages(Level.Error), Is.Empty);
            });
        }
        finally
        {
            _manager?.Dispose();
            System.IO.Directory.Delete(root, true);
        }
    }

    [Test]
    public void CreateXmlToggles_AChangeThatTurnsSitesOff_ClearsTheWrapperCaches()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nr-distro-manager-xml-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        try
        {
            var shippedFile = System.IO.Path.Combine(root, "A.xml");
            System.IO.File.WriteAllText(shippedFile, ShippedXml);
            Mock.Arrange(() => _environment.ExtensionsDirectory).Returns(root);
            _manager = new DistroAgentManager(_environment);
            var shipped = ShippedTable();
            var toggles = _manager.CreateXmlToggles(shipped.Sites, shipped.Manifest, RegisterNothing);
            toggles.ApplyAtStartup();
            var clearedAtStartup = Mock.GetTimesCalled(() => _wrapperService.ClearCaches());

            System.IO.File.Delete(shippedFile);
            toggles.OnChange();

            Assert.Multiple(() =>
            {
                Assert.That(clearedAtStartup, Is.EqualTo(0));
                Assert.That(Enumerable.Range(0, shipped.Sites.Count).Count(shipped.Sites.IsOn), Is.EqualTo(1), "only the plugin site: Run is off with its file deleted");
            });
            Mock.Assert(() => _wrapperService.ClearCaches(), Occurs.Once());
        }
        finally
        {
            _manager?.Dispose();
            System.IO.Directory.Delete(root, true);
        }
    }

    [Test]
    public void CreateXmlToggles_ExtensionsDirectoryNotFound_KeepsDefaultsAndWarnsNamingIt()
    {
        var missing = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nr-distro-manager-missing-" + Guid.NewGuid().ToString("N"));
        Mock.Arrange(() => _environment.ExtensionsDirectory).Returns(missing);
        var logger = RecordingLogger();
        _manager = new DistroAgentManager(_environment);
        var shipped = ShippedTable();

        var registerNow = _manager.CreateXmlToggles(shipped.Sites, shipped.Manifest, RegisterNothing).ApplyAtStartup();

        Assert.Multiple(() =>
        {
            Assert.That(registerNow, Is.EqualTo(new[] { true, true }), "compiled defaults, not 'every shipped file deleted'");
            Assert.That(logger.Messages(Level.Warn), Has.Some.EqualTo("NR instrumentation XML folder " + missing + " was not found; using the distro's compiled instrumentation defaults."));
        });
    }

    [Test]
    public void CreateXmlToggles_SecondCall_ReturnsTheSameInstance()
    {
        _manager = new DistroAgentManager(_environment);
        var shipped = ShippedTable();

        var first = _manager.CreateXmlToggles(shipped.Sites, shipped.Manifest, RegisterNothing);
        var second = _manager.CreateXmlToggles(shipped.Sites, shipped.Manifest, RegisterNothing);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.SameAs(first));
        });
    }

    [Test]
    public void Shutdown_DisposesXmlToggles_AndALaterCreateReturnsNull()
    {
        _manager = new DistroAgentManager(_environment);
        var shipped = ShippedTable();
        var toggles = _manager.CreateXmlToggles(shipped.Sites, shipped.Manifest, RegisterNothing);

        _manager.Shutdown();

        Assert.Multiple(() =>
        {
            Assert.That(toggles.IsDisposed, Is.True);
            Assert.That(_manager.CreateXmlToggles(shipped.Sites, shipped.Manifest, RegisterNothing), Is.Null);
        });
    }

    [Test]
    public void Dispose_DisposesXmlToggles_AndALaterCreateReturnsNull()
    {
        _manager = new DistroAgentManager(_environment);
        var shipped = ShippedTable();
        var toggles = _manager.CreateXmlToggles(shipped.Sites, shipped.Manifest, RegisterNothing);

        _manager.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(toggles.IsDisposed, Is.True);
            Assert.That(_manager.CreateXmlToggles(shipped.Sites, shipped.Manifest, RegisterNothing), Is.Null);
        });
    }

    [Test]
    public void Shutdown_WithoutXmlToggles_DoesNotThrow()
    {
        _manager = new DistroAgentManager(_environment);

        Assert.DoesNotThrow(() => _manager.Shutdown());
    }

    [Test]
    public void CreateXmlToggles_InvalidArguments_LogsErrorAndReturnsNull()
    {
        var logger = RecordingLogger();
        _manager = new DistroAgentManager(_environment);
        var shipped = ShippedTable();

        var toggles = _manager.CreateXmlToggles(null, shipped.Manifest, RegisterNothing);
        var retry = _manager.CreateXmlToggles(shipped.Sites, shipped.Manifest, RegisterNothing);

        Assert.Multiple(() =>
        {
            Assert.That(toggles, Is.Null);
            Assert.That(logger.Messages(Level.Error), Has.Some.Contains("ArgumentNullException"));
            Assert.That(retry, Is.Not.Null, "a failed create does not block a later one");
        });
    }

    [Test]
    public void CreateXmlToggles_InvalidArgumentsAndLoggerThrows_ReturnsNull()
    {
        _manager = new DistroAgentManager(_environment);
        var shipped = ShippedTable();
        var logger = Mock.Create<ILogger>();
        Mock.Arrange(() => logger.Error(Arg.AnyString, Arg.IsAny<object[]>())).Throws(new InvalidOperationException("logger"));
        Log.Initialize(logger);
        _logInitialized = true;

        DistroXmlToggles toggles = null;
        Assert.DoesNotThrow(() => toggles = _manager.CreateXmlToggles(shipped.Sites, null, RegisterNothing));

        Assert.That(toggles, Is.Null);
    }
}
