// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.DependencyInjection;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest;

[TestFixture]
public class AgentManagerBaseTests
{
    private const string FatalConfigurationMessage = "There was a fatal configuration problem preventing the agent from starting. Please see previous log messages for details.";

    private List<string> _calls;
    private IAgentManagerEnvironment _environment;
    private IContainer _container;
    private IAgentHealthReporter _healthReporter;
    private IConfigurationService _configurationService;
    private IAgentApi _agentApi;
    private IWrapperService _wrapperService;
    private IConnectionManager _connectionManager;
    private IBootstrapConfiguration _bootstrapConfig;
    private ILogConfig _logConfig;
    private configuration _localConfig;
    private TestAgentManager _manager;

    private Action<KillAgentEvent> _subscribedKillHandler;
    private EventHandler _subscribedExitHandler;
    private Action<KillAgentEvent> _unsubscribedKillHandler;
    private EventHandler _unsubscribedExitHandler;
    private List<(bool Serverless, bool GcSamplerV2, bool Distro)> _registerFlags;
    private List<(bool Serverless, bool GcSamplerV2, bool Distro)> _startFlags;
    private IBootstrapConfiguration _originalBootstrapConfig;

    [SetUp]
    public void SetUp()
    {
        _calls = new List<string>();
        _registerFlags = new List<(bool, bool, bool)>();
        _startFlags = new List<(bool, bool, bool)>();
        _subscribedKillHandler = null;
        _subscribedExitHandler = null;
        _unsubscribedKillHandler = null;
        _unsubscribedExitHandler = null;

        // Save the real value so TearDown restores it rather than clobbering whatever another fixture left; a
        // test that overrides AdaptBootstrapConfig to touch this static (see OtelDistro.DistroAgentManagerTests
        // for the real override) must not leak into a later fixture.
        _originalBootstrapConfig = ConfigurationLoader.BootstrapConfig;

        _localConfig = new configuration();
        _logConfig = Mock.Create<ILogConfig>();
        _bootstrapConfig = Mock.Create<IBootstrapConfiguration>();
        Mock.Arrange(() => _bootstrapConfig.LogConfig).Returns(_logConfig);
        Mock.Arrange(() => _bootstrapConfig.ServerlessModeEnabled).Returns(false);
        Mock.Arrange(() => _bootstrapConfig.GCSamplerV2Enabled).Returns(true);

        _healthReporter = Mock.Create<IAgentHealthReporter>();
        Mock.Arrange(() => _healthReporter.ValidateAgentConfiguration()).Returns(() => { _calls.Add("ValidateAgentConfiguration"); return true; });
        Mock.Arrange(() => _healthReporter.SetAgentControlStatus(Arg.IsAny<(bool IsHealthy, string Code, string Status)>(), Arg.IsAny<string[]>()))
            .DoInstead(((bool IsHealthy, string Code, string Status) status, string[] statusParams) => _calls.Add("SetAgentControlStatus:" + HealthLabel(status)));
        Mock.Arrange(() => _healthReporter.PublishAgentControlHealthCheck()).DoInstead(() => _calls.Add("PublishAgentControlHealthCheck"));

        _configurationService = Mock.Create<IConfigurationService>();
        _agentApi = Mock.Create<IAgentApi>();
        _wrapperService = Mock.Create<IWrapperService>();
        _connectionManager = Mock.Create<IConnectionManager>();
        Mock.Arrange(() => _connectionManager.AttemptAutoStart()).DoInstead(() => _calls.Add("AttemptAutoStart"));

        _container = Mock.Create<IContainer>();
        Mock.Arrange(() => _container.Resolve<IConfigurationService>()).Returns(() => { _calls.Add("Resolve:IConfigurationService"); return _configurationService; });
        Mock.Arrange(() => _container.Resolve<IAgentHealthReporter>()).Returns(() => { _calls.Add("Resolve:IAgentHealthReporter"); return _healthReporter; });
        Mock.Arrange(() => _container.Resolve<IAgentApi>()).Returns(() => { _calls.Add("Resolve:IAgentApi"); return _agentApi; });
        Mock.Arrange(() => _container.Resolve<IWrapperService>()).Returns(() => { _calls.Add("Resolve:IWrapperService"); return _wrapperService; });
        Mock.Arrange(() => _container.Resolve<IConnectionManager>()).Returns(_connectionManager);

        _environment = Mock.Create<IAgentManagerEnvironment>();
        Mock.Arrange(() => _environment.LoadConfiguration()).Returns(() => { _calls.Add("LoadConfiguration"); return (_localConfig, _bootstrapConfig); });
        Mock.Arrange(() => _environment.ConfigureDefaultLogger()).DoInstead(() => _calls.Add("ConfigureDefaultLogger"));
        Mock.Arrange(() => _environment.ConfigureLogger(Arg.IsAny<ILogConfig>())).DoInstead(() => _calls.Add("ConfigureLogger"));
        Mock.Arrange(() => _environment.CreateContainer()).Returns(() => { _calls.Add("CreateContainer"); return _container; });
        Mock.Arrange(() => _environment.RegisterServices(Arg.IsAny<IContainer>(), Arg.AnyBool, Arg.AnyBool, Arg.AnyBool))
            .DoInstead((IContainer c, bool serverless, bool gcSamplerV2, bool distro) => { _calls.Add("RegisterServices"); _registerFlags.Add((serverless, gcSamplerV2, distro)); });
        Mock.Arrange(() => _environment.StartServices(Arg.IsAny<IContainer>(), Arg.AnyBool, Arg.AnyBool, Arg.AnyBool))
            .DoInstead((IContainer c, bool serverless, bool gcSamplerV2, bool distro) => { _calls.Add("StartServices"); _startFlags.Add((serverless, gcSamplerV2, distro)); });
        Mock.Arrange(() => _environment.PublishDeserializedConfiguration(Arg.IsAny<configuration>())).DoInstead(() => _calls.Add("PublishDeserializedConfiguration"));
        Mock.Arrange(() => _environment.SubscribeToKillAgent(Arg.IsAny<Action<KillAgentEvent>>()))
            .DoInstead((Action<KillAgentEvent> handler) => { _calls.Add("SubscribeToKillAgent"); _subscribedKillHandler = handler; });
        Mock.Arrange(() => _environment.UnsubscribeFromKillAgent(Arg.IsAny<Action<KillAgentEvent>>()))
            .DoInstead((Action<KillAgentEvent> handler) => { _calls.Add("UnsubscribeFromKillAgent"); _unsubscribedKillHandler = handler; });
        Mock.Arrange(() => _environment.ExtensionsDirectory).Returns(@"C:\nr\extensions");
        Mock.Arrange(() => _environment.InitializeExtensions(Arg.AnyString)).DoInstead(() => _calls.Add("InitializeExtensions"));
        Mock.Arrange(() => _environment.SetAgentApiImplementation(Arg.IsAny<IAgentApi>())).DoInstead(() => _calls.Add("SetAgentApiImplementation"));
        Mock.Arrange(() => _environment.SubscribeToExit(Arg.IsAny<EventHandler>()))
            .DoInstead((EventHandler handler) => { _calls.Add("SubscribeToExit"); _subscribedExitHandler = handler; });
        Mock.Arrange(() => _environment.UnsubscribeFromExit(Arg.IsAny<EventHandler>()))
            .DoInstead((EventHandler handler) => { _calls.Add("UnsubscribeFromExit"); _unsubscribedExitHandler = handler; });
        Mock.Arrange(() => _environment.MarkAgentShuttingDown()).DoInstead(() => _calls.Add("MarkAgentShuttingDown"));
        Mock.Arrange(() => _environment.PublishCleanShutdownEvents()).DoInstead(() => _calls.Add("PublishCleanShutdownEvents"));
        Mock.Arrange(() => _environment.CloseLogging()).DoInstead(() => _calls.Add("CloseLogging"));

        _manager = new TestAgentManager(_environment, _calls);
    }

    [TearDown]
    public void TearDown()
    {
        // Releases the configuration subscription the base created, for tests that never shut down.
        _manager?.Dispose();

        // Mocks; disposing them is a no-op, but satisfies NUnit1032.
        _container?.Dispose();
        _connectionManager?.Dispose();

        ConfigurationLoader.UseBootstrapConfigurationForTesting(_originalBootstrapConfig);
    }

    [Test]
    public void Ctor_NullEnvironment_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TestAgentManager(null, _calls));
    }

    [Test]
    public void Ctor_DoesNothingButStoreTheEnvironment()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_calls, Is.Empty);
            Assert.That(_manager.ExposedEnvironment, Is.SameAs(_environment));
            Assert.That(_manager.ExposedContainer, Is.Null);
            Assert.That(_manager.IsInitialized, Is.False);
            Assert.That(_manager.ShutdownEventReceived, Is.False);
            Assert.That(_manager.ExposedConfiguration, Is.Not.Null);
        });
    }

    [Test]
    public void Start_RunsStepsInOrder()
    {
        _manager.RunStart();

        Assert.Multiple(() =>
        {
            Assert.That(_calls, Is.EqualTo(new[]
            {
                "LoadConfiguration", "CreateContainer", "ResolveServerlessMode", "RegisterServices",
                "Resolve:IConfigurationService", "PublishDeserializedConfiguration", "AfterConfigurationPublished",
                "ConfigureLogger", "Resolve:IAgentHealthReporter", "ValidateAgentConfiguration", "SubscribeToKillAgent",
                "InitializeExtensions", "Resolve:IAgentApi", "Resolve:IWrapperService", "AfterWrapperServiceResolved",
                "AttemptAutoStart", "StartServices", "SetAgentApiImplementation", "AfterAgentApiSet",
                "SubscribeToExit", "StartModeServices"
            }));
            Assert.That(_manager.IsInitialized, Is.True);
        });
    }

    [Test]
    public void Start_PopulatesProtectedState()
    {
        _manager.RunStart();

        Assert.Multiple(() =>
        {
            Assert.That(_manager.ExposedContainer, Is.SameAs(_container));
            Assert.That(_manager.ExposedHealthReporter, Is.SameAs(_healthReporter));
            Assert.That(_manager.ExposedAgentApi, Is.SameAs(_agentApi));
            Assert.That(_manager.ExposedBootstrapConfig, Is.SameAs(_bootstrapConfig));
            Assert.That(_manager.ExposedServerlessModeEnabled, Is.False);
            Assert.That(_manager.ExposedExtensionsDirectory, Is.EqualTo(@"C:\nr\extensions"));
            Assert.That(_manager.ResolvedWrapperService, Is.SameAs(_wrapperService));
        });
        Mock.Assert(() => _environment.PublishDeserializedConfiguration(_localConfig), Occurs.Once());
        Mock.Assert(() => _environment.ConfigureLogger(_logConfig), Occurs.Once());
        Mock.Assert(() => _environment.InitializeExtensions(@"C:\nr\extensions"), Occurs.Once());
        Mock.Assert(() => _environment.SetAgentApiImplementation(_agentApi), Occurs.Once());
        Mock.Assert(() => _environment.RegisterServices(_container, Arg.AnyBool, Arg.AnyBool, Arg.AnyBool), Occurs.Once());
        Mock.Assert(() => _environment.StartServices(_container, Arg.AnyBool, Arg.AnyBool, Arg.AnyBool), Occurs.Once());
    }

    // AdaptBootstrapConfig is the seam DistroAgentManager overrides (OtelDistro.DistroAgentManagerTests covers
    // that override end-to-end); this base class must stay distro-free, so these tests only prove the hook is
    // called once with the loaded config and that its result -- unmodified by default -- becomes BootstrapConfig.
    [Test]
    public void Start_AdaptBootstrapConfig_DefaultIsPassthrough()
    {
        _manager.RunStart();

        Assert.That(_manager.ExposedBootstrapConfig, Is.SameAs(_bootstrapConfig));
    }

    [Test]
    public void Start_AdaptBootstrapConfig_OverrideResultBecomesBootstrapConfig()
    {
        var replacement = Mock.Create<IBootstrapConfiguration>();
        IBootstrapConfiguration passedIn = null;
        _manager.AdaptBootstrapConfigOverride = c => { passedIn = c; return replacement; };

        _manager.RunStart();

        Assert.Multiple(() =>
        {
            Assert.That(passedIn, Is.SameAs(_bootstrapConfig));
            Assert.That(_manager.ExposedBootstrapConfig, Is.SameAs(replacement));
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public void Start_PassesModeFlagsToRegisterAndStartServices(bool serverless, bool distro)
    {
        Mock.Arrange(() => _bootstrapConfig.ServerlessModeEnabled).Returns(serverless);
        _manager.Distro = distro;

        _manager.RunStart();

        var expected = (serverless, true, distro);
        Assert.Multiple(() =>
        {
            Assert.That(_registerFlags, Is.EqualTo(new[] { expected }));
            Assert.That(_startFlags, Is.EqualTo(new[] { expected }));
            Assert.That(_manager.ExposedServerlessModeEnabled, Is.EqualTo(serverless));
        });
    }

    [Test]
    public void Start_UsesResolveServerlessModeOverride()
    {
        _manager.ServerlessOverride = true;

        _manager.RunStart();

        Assert.Multiple(() =>
        {
            Assert.That(_registerFlags, Is.EqualTo(new[] { (true, true, false) }));
            Assert.That(_calls, Does.Not.Contain("AttemptAutoStart"));
        });
    }

    [Test]
    public void Start_Serverless_SkipsConnect()
    {
        Mock.Arrange(() => _bootstrapConfig.ServerlessModeEnabled).Returns(true);

        _manager.RunStart();

        Assert.Multiple(() =>
        {
            Assert.That(_calls, Does.Not.Contain("AttemptAutoStart"));
            Assert.That(_manager.IsInitialized, Is.True);
        });
        Mock.Assert(() => _connectionManager.AttemptAutoStart(), Occurs.Never());
        Mock.Assert(() => _container.Resolve<IConnectionManager>(), Occurs.Never());
    }

    [Test]
    public void Start_WhenLoadConfigurationThrows_ConfiguresDefaultLoggerAndRethrows()
    {
        var expected = new InvalidOperationException("config load failed");
        Mock.Arrange(() => _environment.LoadConfiguration()).Throws(expected);

        var thrown = Assert.Throws<InvalidOperationException>(() => _manager.RunStart());

        Assert.Multiple(() =>
        {
            Assert.That(thrown, Is.SameAs(expected));
            Assert.That(_calls, Is.EqualTo(new[] { "ConfigureDefaultLogger" }));
            Assert.That(_calls, Does.Not.Contain("CreateContainer"));
            Assert.That(_calls, Does.Not.Contain("OnStartFailed"));
            Assert.That(_manager.IsInitialized, Is.False);
        });
    }

    [Test]
    public void Start_WhenDefaultLoggerAlsoThrows_RethrowsOriginal()
    {
        var expected = new InvalidOperationException("config load failed");
        Mock.Arrange(() => _environment.LoadConfiguration()).Throws(expected);
        Mock.Arrange(() => _environment.ConfigureDefaultLogger()).Throws(new ApplicationException("logger failed"));

        var thrown = Assert.Throws<InvalidOperationException>(() => _manager.RunStart());

        Assert.Multiple(() =>
        {
            Assert.That(thrown, Is.SameAs(expected));
            Assert.That(_calls, Does.Not.Contain("CreateContainer"));
            Assert.That(_calls, Does.Not.Contain("OnStartFailed"));
        });
    }

    [Test]
    public void Start_WhenConfigurationInvalid_ShutsDownAndThrows()
    {
        Mock.Arrange(() => _healthReporter.ValidateAgentConfiguration()).Returns(() => { _calls.Add("ValidateAgentConfiguration"); return false; });

        var thrown = Assert.Throws<Exception>(() => _manager.RunStart());

        Assert.Multiple(() =>
        {
            Assert.That(thrown.Message, Is.EqualTo(FatalConfigurationMessage));
            Assert.That(_calls, Does.Contain("MarkAgentShuttingDown"));
            Assert.That(_calls, Does.Contain("Dispose"));
            Assert.That(_calls, Does.Not.Contain("PublishCleanShutdownEvents"));
            Assert.That(_calls, Does.Not.Contain("SubscribeToKillAgent"));
            Assert.That(_calls.FindAll(c => c == "OnStartFailed"), Has.Count.EqualTo(1));
            Assert.That(_calls[_calls.Count - 1], Is.EqualTo("OnStartFailed"));
            Assert.That(_manager.IsInitialized, Is.False);
        });
    }

    [Test]
    public void Start_WhenStepThrows_CallsOnStartFailedAndRethrows()
    {
        var expected = new InvalidOperationException("connection manager failed");
        Mock.Arrange(() => _container.Resolve<IConnectionManager>()).Throws(expected);

        var thrown = Assert.Throws<InvalidOperationException>(() => _manager.RunStart());

        Assert.Multiple(() =>
        {
            Assert.That(thrown, Is.SameAs(expected));
            Assert.That(_calls.FindAll(c => c == "OnStartFailed"), Has.Count.EqualTo(1));
            Assert.That(_calls, Does.Not.Contain("SubscribeToExit"));
            Assert.That(_calls, Does.Not.Contain("MarkAgentShuttingDown"));
            Assert.That(_manager.IsInitialized, Is.False);
        });
    }

    [Test]
    public void Start_WhenModeStepThrows_CallsOnStartFailedAndRethrows()
    {
        var expected = new InvalidOperationException("mode step failed");
        _manager.ThrowFrom = "StartModeServices";
        _manager.ExceptionToThrow = expected;

        var thrown = Assert.Throws<InvalidOperationException>(() => _manager.RunStart());

        Assert.Multiple(() =>
        {
            Assert.That(thrown, Is.SameAs(expected));
            Assert.That(_calls.FindAll(c => c == "OnStartFailed"), Has.Count.EqualTo(1));
            Assert.That(_manager.IsInitialized, Is.False);
        });
    }

    [Test]
    public void KillAgentEvent_BeforeInitialized_OnlyRecordsIt()
    {
        _manager.OnAfterWrapperServiceResolved = () => _subscribedKillHandler(new KillAgentEvent());

        _manager.RunStart();

        Assert.Multiple(() =>
        {
            Assert.That(_manager.ShutdownEventReceived, Is.True);
            Assert.That(_manager.IsInitialized, Is.True);
            Assert.That(_calls, Does.Not.Contain("MarkAgentShuttingDown"));
        });
    }

    [Test]
    public void KillAgentEvent_AfterInitialized_ShutsDownNotClean()
    {
        _manager.RunStart();
        _calls.Clear();

        _subscribedKillHandler(new KillAgentEvent());

        Assert.Multiple(() =>
        {
            Assert.That(_manager.ShutdownEventReceived, Is.True);
            Assert.That(_calls, Does.Contain("MarkAgentShuttingDown"));
            Assert.That(_calls, Does.Not.Contain("PublishCleanShutdownEvents"));
            Assert.That(_calls, Does.Contain("Dispose"));
            Assert.That(_calls, Does.Contain("CloseLogging"));
        });
    }

    [Test]
    public void ExitHook_ShutsDownClean()
    {
        _manager.RunStart();
        _calls.Clear();

        _subscribedExitHandler(this, EventArgs.Empty);

        Assert.That(_calls, Is.EqualTo(new[]
        {
            "MarkAgentShuttingDown", "BeforeShutdown", "UnsubscribeFromExit", "PublishCleanShutdownEvents",
            "StopModeServices", "SetAgentControlStatus:Healthy", "PublishAgentControlHealthCheck", "Dispose", "CloseLogging"
        }));
        Assert.That(_unsubscribedExitHandler, Is.EqualTo(_subscribedExitHandler));
        Mock.Assert(() => _container.Dispose(), Occurs.Once());
    }

    [Test]
    public void Shutdown_WhenStopModeServicesThrows_ReportsErrorAndStillDisposes()
    {
        string[] errorParams = null;
        Mock.Arrange(() => _healthReporter.SetAgentControlStatus(HealthCodes.AgentShutdownError, Arg.IsAny<string[]>()))
            .DoInstead(((bool IsHealthy, string Code, string Status) status, string[] statusParams) => { _calls.Add("SetAgentControlStatus:Error"); errorParams = statusParams; });
        _manager.RunStart();
        _calls.Clear();
        _manager.ThrowFrom = "StopModeServices";
        _manager.ExceptionToThrow = new InvalidOperationException("stop failed");

        Assert.DoesNotThrow(() => _manager.RunShutdown(true));

        Assert.Multiple(() =>
        {
            Assert.That(_calls, Does.Contain("SetAgentControlStatus:Error"));
            Assert.That(_calls, Does.Not.Contain("SetAgentControlStatus:Healthy"));
            Assert.That(errorParams, Is.EqualTo(new[] { "stop failed" }));
            Assert.That(_calls, Does.Contain("PublishAgentControlHealthCheck"));
            Assert.That(_calls, Does.Contain("Dispose"));
            Assert.That(_calls, Does.Contain("CloseLogging"));
        });
    }

    [Test]
    public void Shutdown_CloseLoggingFalse_DoesNotCloseLogging()
    {
        _manager.RunStart();
        _calls.Clear();

        _manager.RunShutdown(false, closeLogging: false);

        Assert.Multiple(() =>
        {
            Assert.That(_calls, Does.Contain("Dispose"));
            Assert.That(_calls, Does.Not.Contain("CloseLogging"));
            Assert.That(_calls, Does.Not.Contain("PublishCleanShutdownEvents"));
        });
    }

    [Test]
    public void Shutdown_BeforeHealthReporterExists_DoesNotThrow()
    {
        Mock.Arrange(() => _environment.RegisterServices(Arg.IsAny<IContainer>(), Arg.AnyBool, Arg.AnyBool, Arg.AnyBool))
            .Throws(new InvalidOperationException("register failed"));
        Assert.Throws<InvalidOperationException>(() => _manager.RunStart());
        _calls.Clear();

        Assert.DoesNotThrow(() => _manager.RunShutdown(false));

        Assert.Multiple(() =>
        {
            Assert.That(_manager.ExposedHealthReporter, Is.Null);
            Assert.That(_calls, Does.Contain("Dispose"));
            Assert.That(_calls, Does.Not.Contain("PublishAgentControlHealthCheck"));
        });
    }

    [Test]
    public void Dispose_BeforeStart_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _manager.Dispose());
    }

    [Test]
    public void UnsubscribeFromAgentEvents_UnsubscribesBothHandlers()
    {
        _manager.RunStart();
        _calls.Clear();

        _manager.RunUnsubscribeFromAgentEvents();

        Assert.Multiple(() =>
        {
            Assert.That(_calls, Is.EqualTo(new[] { "UnsubscribeFromKillAgent", "UnsubscribeFromExit" }));
            Assert.That(_unsubscribedKillHandler, Is.Not.Null);
            Assert.That(_unsubscribedKillHandler, Is.EqualTo(_subscribedKillHandler));
            Assert.That(_unsubscribedExitHandler, Is.Not.Null);
            Assert.That(_unsubscribedExitHandler, Is.EqualTo(_subscribedExitHandler));
        });
    }

    private static string HealthLabel((bool IsHealthy, string Code, string Status) status)
    {
        if (status == HealthCodes.AgentShutdownHealthy)
            return "Healthy";
        if (status == HealthCodes.AgentShutdownError)
            return "Error";
        return status.Code;
    }

    private sealed class TestAgentManager : AgentManagerBase
    {
        private readonly List<string> _calls;

        public bool Distro { get; set; }
        public bool? ServerlessOverride { get; set; }
        public string ThrowFrom { get; set; }
        public Exception ExceptionToThrow { get; set; }
        public Action OnAfterWrapperServiceResolved { get; set; }
        public Func<IBootstrapConfiguration, IBootstrapConfiguration> AdaptBootstrapConfigOverride { get; set; }
        public IWrapperService ResolvedWrapperService { get; private set; }

        public IAgentManagerEnvironment ExposedEnvironment => Environment;
        public IContainer ExposedContainer => Container;
        public IAgentHealthReporter ExposedHealthReporter => HealthReporter;
        public IAgentApi ExposedAgentApi => AgentApi;
        public IBootstrapConfiguration ExposedBootstrapConfig => BootstrapConfig;
        public bool ExposedServerlessModeEnabled => ServerlessModeEnabled;
        public string ExposedExtensionsDirectory => ExtensionsDirectory;
        public IConfiguration ExposedConfiguration => Configuration;

        protected override bool DistroModeEnabled => Distro;

        protected override IBootstrapConfiguration AdaptBootstrapConfig(IBootstrapConfiguration bootstrapConfig)
            => AdaptBootstrapConfigOverride != null ? AdaptBootstrapConfigOverride(bootstrapConfig) : base.AdaptBootstrapConfig(bootstrapConfig);

        public TestAgentManager(IAgentManagerEnvironment environment, List<string> calls) : base(environment)
        {
            _calls = calls;
        }

        public void RunStart() => Start();

        public void RunShutdown(bool cleanShutdown, bool closeLogging = true) => Shutdown(cleanShutdown, closeLogging);

        public void RunUnsubscribeFromAgentEvents() => UnsubscribeFromAgentEvents();

        public override void Dispose()
        {
            _calls?.Add("Dispose");
            base.Dispose();
        }

        protected override bool ResolveServerlessMode(IBootstrapConfiguration c)
        {
            Record(nameof(ResolveServerlessMode));
            return ServerlessOverride ?? base.ResolveServerlessMode(c);
        }

        protected override void AfterConfigurationPublished()
        {
            Record(nameof(AfterConfigurationPublished));
            base.AfterConfigurationPublished();
        }

        protected override void AfterWrapperServiceResolved(IWrapperService wrapperService)
        {
            Record(nameof(AfterWrapperServiceResolved));
            ResolvedWrapperService = wrapperService;
            OnAfterWrapperServiceResolved?.Invoke();
            base.AfterWrapperServiceResolved(wrapperService);
        }

        protected override void AfterAgentApiSet()
        {
            Record(nameof(AfterAgentApiSet));
            base.AfterAgentApiSet();
        }

        protected override void StartModeServices()
        {
            Record(nameof(StartModeServices));
            base.StartModeServices();
        }

        protected override void OnStartFailed()
        {
            Record(nameof(OnStartFailed));
            base.OnStartFailed();
        }

        protected override void BeforeShutdown()
        {
            Record(nameof(BeforeShutdown));
            base.BeforeShutdown();
        }

        protected override void StopModeServices()
        {
            Record(nameof(StopModeServices));
            base.StopModeServices();
        }

        private void Record(string name)
        {
            _calls.Add(name);
            if (ThrowFrom == name)
                throw ExceptionToThrow;
        }
    }
}
