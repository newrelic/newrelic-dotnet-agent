// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
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
}
