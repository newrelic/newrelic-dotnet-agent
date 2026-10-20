// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Net;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Labels;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;
using Newtonsoft.Json;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DataTransport.Tests;

[TestFixture]
public class ConnectionHandlerTests
{
    private ISerializer _serializer;
    private ICollectorWireFactory _collectorWireFactory;
    private IProcessStatic _processStatic;
    private IDnsStatic _dnsStatic;
    private ILabelsService _labelsService;
    private ISystemInfo _systemInfo;
    private Environment _environment;
    private IAgentHealthReporter _agentHealthReporter;
    private IEnvironment _environmentVariableHelper;
    private IConfiguration _configuration;
    private ConnectionHandler _connectionHandler;
    private ICollectorWire _dataRequestWire;
    private IFileWrapper _fileWrapper;
    private ILogger _nrLogger;

    [SetUp]
    public void SetUp()
    {
        _serializer = Mock.Create<ISerializer>();
        _collectorWireFactory = Mock.Create<ICollectorWireFactory>();
        _processStatic = Mock.Create<IProcessStatic>();
        _dnsStatic = Mock.Create<IDnsStatic>();
        _labelsService = Mock.Create<ILabelsService>();
        _systemInfo = Mock.Create<ISystemInfo>();
        _environment = Mock.Create<Environment>();
        _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
        _environmentVariableHelper = Mock.Create<IEnvironment>();
        _configuration = Mock.Create<IConfiguration>();
        _dataRequestWire = Mock.Create<ICollectorWire>();
        _fileWrapper = Mock.Create<IFileWrapper>();

        _nrLogger = Mock.Create<ILogger>();
        Log.Initialize(_nrLogger);

        Mock.Arrange(() => _configuration.SecurityPoliciesTokenExists).Returns(false);
        Mock.Arrange(() => _configuration.ApplicationNames).Returns(new List<string> { "TestApp" });
        Mock.Arrange(() => _configuration.AgentRunId).Returns("12345");
        Mock.Arrange(() => _configuration.ProcessHostDisplayName).Returns("processHostDisplayName");
        Mock.Arrange(() => _dnsStatic.GetHostName()).Returns("dnsStaticHostName");

        _connectionHandler = new ConnectionHandler(
            _serializer,
            _collectorWireFactory,
            _processStatic,
            _dnsStatic,
            _labelsService,
            _environment,
            _systemInfo,
            _agentHealthReporter,
            _environmentVariableHelper,
            _fileWrapper,
            _dataRequestWire
        );

        _connectionHandler.OverrideConfigForTesting(_configuration);
    }

    [TearDown]
    public void TearDown()
    {
        _connectionHandler.Dispose();
        _labelsService.Dispose();
        Log.Initialize(new NoOpLogger());
    }

    [Test]
    public void Connect_ShouldSetAgentControlStatusToHealthy_OnSuccess()
    {
        // Arrange
        var preconnectResult = new PreconnectResult { RedirectHost = "redirectHost" };

        // create and populate a server configuration
        var serverConfiguration = new ServerConfiguration
        {
            AgentRunId = "12345",
        };

        var collectorWire = Mock.Create<ICollectorWire>();
        Mock.Arrange(() => _collectorWireFactory.GetCollectorWire(Arg.IsAny<IConfiguration>(), Arg.IsAny<IAgentHealthReporter>()))
            .Returns(collectorWire);

        Mock.Arrange(() => _serializer.Serialize(Arg.IsAny<object[]>())).Returns("serializedData");
        Mock.Arrange(() => _serializer.Deserialize<CollectorResponseEnvelope<PreconnectResult>>(Arg.IsAny<string>())).Returns(new CollectorResponseEnvelope<PreconnectResult>(preconnectResult));
        Mock.Arrange(() => _serializer.Deserialize<CollectorResponseEnvelope<Dictionary<string, object>>>(Arg.IsAny<string>()))
            .Returns(new CollectorResponseEnvelope<Dictionary<string, object>>(
                JsonConvert.DeserializeObject<Dictionary<string, object>>(
                    JsonConvert.SerializeObject(serverConfiguration))));

        // Act
        _connectionHandler.Connect();

        // Assert
        Mock.Assert(() => _agentHealthReporter.SetAgentControlStatus(HealthCodes.Healthy), Occurs.Once());
    }

    [Test]
    public void Connect_ShouldLogDeprecationMessage_WhenSecurityPoliciesTokenExists()
    {
        // Arrange
        Mock.Arrange(() => _configuration.SecurityPoliciesTokenExists).Returns(true);

        var preconnectResult = new PreconnectResult { RedirectHost = "redirectHost" };

        var serverConfiguration = new ServerConfiguration
        {
            AgentRunId = "12345",
        };

        var collectorWire = Mock.Create<ICollectorWire>();
        Mock.Arrange(() => _collectorWireFactory.GetCollectorWire(Arg.IsAny<IConfiguration>(), Arg.IsAny<IAgentHealthReporter>()))
            .Returns(collectorWire);

        Mock.Arrange(() => _serializer.Serialize(Arg.IsAny<object[]>())).Returns("serializedData");
        Mock.Arrange(() => _serializer.Deserialize<CollectorResponseEnvelope<PreconnectResult>>(Arg.IsAny<string>())).Returns(new CollectorResponseEnvelope<PreconnectResult>(preconnectResult));
        Mock.Arrange(() => _serializer.Deserialize<CollectorResponseEnvelope<Dictionary<string, object>>>(Arg.IsAny<string>()))
            .Returns(new CollectorResponseEnvelope<Dictionary<string, object>>(
                JsonConvert.DeserializeObject<Dictionary<string, object>>(
                    JsonConvert.SerializeObject(serverConfiguration))));

        // Act
        _connectionHandler.Connect();

        // Assert
        Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => m.Contains("securityPoliciesToken")), Arg.IsAny<object[]>()), Occurs.Once());
    }

    [Test]
    public void Connect_ShouldNotLogDeprecationMessage_WhenSecurityPoliciesTokenDoesNotExist()
    {
        // Arrange
        Mock.Arrange(() => _configuration.SecurityPoliciesTokenExists).Returns(false);

        var preconnectResult = new PreconnectResult { RedirectHost = "redirectHost" };

        var serverConfiguration = new ServerConfiguration
        {
            AgentRunId = "12345",
        };

        var collectorWire = Mock.Create<ICollectorWire>();
        Mock.Arrange(() => _collectorWireFactory.GetCollectorWire(Arg.IsAny<IConfiguration>(), Arg.IsAny<IAgentHealthReporter>()))
            .Returns(collectorWire);

        Mock.Arrange(() => _serializer.Serialize(Arg.IsAny<object[]>())).Returns("serializedData");
        Mock.Arrange(() => _serializer.Deserialize<CollectorResponseEnvelope<PreconnectResult>>(Arg.IsAny<string>())).Returns(new CollectorResponseEnvelope<PreconnectResult>(preconnectResult));
        Mock.Arrange(() => _serializer.Deserialize<CollectorResponseEnvelope<Dictionary<string, object>>>(Arg.IsAny<string>()))
            .Returns(new CollectorResponseEnvelope<Dictionary<string, object>>(
                JsonConvert.DeserializeObject<Dictionary<string, object>>(
                    JsonConvert.SerializeObject(serverConfiguration))));

        // Act
        _connectionHandler.Connect();

        // Assert
        Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => m.Contains("securityPoliciesToken")), Arg.IsAny<object[]>()), Occurs.Never());
    }

    [TestCase("1.317.0", true, "rum", null, null, true, TestName = "Connect_ShouldWarn_WhenVersionSetAndNoLoaderReturned")]
    [TestCase("1.317.0", true, "full", null, "", true, TestName = "Connect_ShouldWarn_WhenVersionSetAndEmptyLoaderReturned")]
    [TestCase("1.317.0", true, "none", "rum", null, true, TestName = "Connect_ShouldWarn_WhenServerLoaderTypeOverridesLocalNone")]
    [TestCase("1.317.0", true, "rum", null, "loader-js", false, TestName = "Connect_ShouldNotWarn_WhenLoaderReturned")]
    [TestCase("", true, "rum", null, null, false, TestName = "Connect_ShouldNotWarn_WhenVersionEmpty")]
    [TestCase(null, true, "rum", null, null, false, TestName = "Connect_ShouldNotWarn_WhenVersionNull")]
    [TestCase("1.317.0", false, "rum", null, null, false, TestName = "Connect_ShouldNotWarn_WhenAutoInstrumentDisabled")]
    [TestCase("1.317.0", true, "none", null, null, false, TestName = "Connect_ShouldNotWarn_WhenLocalLoaderTypeNone")]
    [TestCase("1.317.0", true, "rum", "NONE", null, false, TestName = "Connect_ShouldNotWarn_WhenServerLoaderTypeNoneIgnoringCase")]
    public void Connect_BrowserAgentVersionWarning(string version, bool autoInstrument, string localLoaderType, string serverLoaderType, string loaderScript, bool expectWarning)
    {
        // Arrange
        Mock.Arrange(() => _configuration.BrowserMonitoringVersion).Returns(version);
        Mock.Arrange(() => _configuration.BrowserMonitoringAutoInstrument).Returns(autoInstrument);
        Mock.Arrange(() => _configuration.BrowserMonitoringJavaScriptAgentLoaderType).Returns(localLoaderType);

        ArrangeSuccessfulConnect(new ServerConfiguration
        {
            AgentRunId = "12345",
            RumSettingsBrowserMonitoringLoader = serverLoaderType,
            RumSettingsJavaScriptAgentLoader = loaderScript
        });

        // Act
        _connectionHandler.Connect();

        // Assert
        var occurs = expectWarning ? Occurs.Once() : Occurs.Never();
        Mock.Assert(() => _nrLogger.Warn(Arg.Matches<string>(m => m.Contains("browser_monitoring.version")), Arg.IsAny<object[]>()), occurs);
    }

    [Test]
    public void Connect_BrowserAgentVersionWarning_NamesRequestedVersion()
    {
        // Arrange
        Mock.Arrange(() => _configuration.BrowserMonitoringVersion).Returns("1.317.0");
        Mock.Arrange(() => _configuration.BrowserMonitoringAutoInstrument).Returns(true);
        Mock.Arrange(() => _configuration.BrowserMonitoringJavaScriptAgentLoaderType).Returns("rum");

        ArrangeSuccessfulConnect(new ServerConfiguration { AgentRunId = "12345" });

        // Act
        _connectionHandler.Connect();

        // Assert
        Mock.Assert(() => _nrLogger.Warn(Arg.IsAny<string>(), Arg.Matches<object[]>(a => a.Length == 1 && (string)a[0] == "1.317.0")), Occurs.Once());
    }

    private void ArrangeSuccessfulConnect(ServerConfiguration serverConfiguration)
    {
        var preconnectResult = new PreconnectResult { RedirectHost = "redirectHost" };

        var collectorWire = Mock.Create<ICollectorWire>();
        Mock.Arrange(() => _collectorWireFactory.GetCollectorWire(Arg.IsAny<IConfiguration>(), Arg.IsAny<IAgentHealthReporter>()))
            .Returns(collectorWire);

        Mock.Arrange(() => _serializer.Serialize(Arg.IsAny<object[]>())).Returns("serializedData");
        Mock.Arrange(() => _serializer.Deserialize<CollectorResponseEnvelope<PreconnectResult>>(Arg.IsAny<string>())).Returns(new CollectorResponseEnvelope<PreconnectResult>(preconnectResult));
        Mock.Arrange(() => _serializer.Deserialize<CollectorResponseEnvelope<Dictionary<string, object>>>(Arg.IsAny<string>()))
            .Returns(new CollectorResponseEnvelope<Dictionary<string, object>>(
                JsonConvert.DeserializeObject<Dictionary<string, object>>(
                    JsonConvert.SerializeObject(serverConfiguration))));
    }

    [Test]
    public void Connect_ShouldSetAgentControlStatusToFailedToConnect_OnFailure()
    {
        // Arrange
        var preconnectResult = new PreconnectResult { RedirectHost = "redirectHost" };

        var collectorWire = Mock.Create<ICollectorWire>();
        Mock.Arrange(() =>
                _collectorWireFactory.GetCollectorWire(Arg.IsAny<IConfiguration>(),
                    Arg.IsAny<IAgentHealthReporter>()))
            .Returns(collectorWire);

        Mock.Arrange(() => _serializer.Serialize(Arg.IsAny<object[]>())).Returns("serializedData");
        Mock.Arrange(
                () => _serializer.Deserialize<CollectorResponseEnvelope<PreconnectResult>>(Arg.IsAny<string>()))
            .Returns(new CollectorResponseEnvelope<PreconnectResult>(preconnectResult));

        Mock.Arrange(() => collectorWire.SendData("connect", Arg.IsAny<ConnectionInfo>(),
                Arg.IsAny<string>(), Arg.IsAny<Guid>()))
            .Throws(new Exception("connection failed"));

        // Act & assert
        var ex = Assert.Throws<Exception>(() =>_connectionHandler.Connect());
        Assert.That(ex.Message, Is.EqualTo("connection failed"));
        Mock.Assert(() => _agentHealthReporter.SetAgentControlStatus(HealthCodes.FailedToConnect), Occurs.Once());
    }

    [Test]
    public void SendDataRequest_ShouldSetAgentControlStatusToHttpError_OnHttpException()
    {
        // Arrange
        Mock.Arrange(() => _dataRequestWire.SendData(Arg.IsAny<string>(), Arg.IsAny<ConnectionInfo>(),
                Arg.IsAny<string>(), Arg.IsAny<Guid>()))
            .Throws(new DataTransport.HttpException(HttpStatusCode.InternalServerError, "Internal Server Error"));


        // Act & Assert
        var ex = Assert.Throws<DataTransport.HttpException>(() => _connectionHandler.SendDataRequest<object>("testMethod"));
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));

        Mock.Assert(() => _agentHealthReporter.SetAgentControlStatus(HealthCodes.HttpError, "InternalServerError", "testMethod"), Occurs.Once());
    }
    [Test]
    public void SendDataRequest_ShouldSetAgentControlStatusToLicenseKeyInvalid_OnUnauthorized()
    {
        // Arrange
        Mock.Arrange(() => _dataRequestWire.SendData(Arg.IsAny<string>(), Arg.IsAny<ConnectionInfo>(),
                Arg.IsAny<string>(), Arg.IsAny<Guid>()))
            .Throws(new DataTransport.HttpException(HttpStatusCode.Unauthorized, "Unauthorized"));

        // Act & Assert
        var ex = Assert.Throws<DataTransport.HttpException>(() => _connectionHandler.SendDataRequest<object>("testMethod"));
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        Mock.Assert(() => _agentHealthReporter.SetAgentControlStatus(HealthCodes.LicenseKeyInvalid), Occurs.Once());
    }

    [Test]
    public void SendDataRequest_ShouldSetAgentControlStatusToForceDisconnect_OnGone()
    {
        // Arrange
        Mock.Arrange(() => _dataRequestWire.SendData(Arg.IsAny<string>(), Arg.IsAny<ConnectionInfo>(), Arg.IsAny<string>(), Arg.IsAny<Guid>()))
            .Throws(new DataTransport.HttpException(HttpStatusCode.Gone, "Gone"));

        // Act & Assert
        var ex = Assert.Throws<DataTransport.HttpException>(() => _connectionHandler.SendDataRequest<object>("testMethod"));
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.Gone));

        Mock.Assert(() => _agentHealthReporter.SetAgentControlStatus(HealthCodes.ForceDisconnect), Occurs.Once());
    }

    [Test]
    public void SendDataRequest_ShouldSetAgentControlStatusToHttpProxyError_OnProxyAuthenticationRequired()
    {
        // Arrange
        Mock.Arrange(() => _dataRequestWire.SendData(Arg.IsAny<string>(), Arg.IsAny<ConnectionInfo>(), Arg.IsAny<string>(), Arg.IsAny<Guid>()))
            .Throws(new DataTransport.HttpException(HttpStatusCode.ProxyAuthenticationRequired, "Proxy Authentication Required"));

        // Act & Assert
        var ex = Assert.Throws<DataTransport.HttpException>(() => _connectionHandler.SendDataRequest<object>("testMethod"));
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.ProxyAuthenticationRequired));

        Mock.Assert(() => _agentHealthReporter.SetAgentControlStatus(HealthCodes.HttpProxyError, "ProxyAuthenticationRequired"), Occurs.Once());
    }

    [Test]
    public void SendDataRequest_ShouldSetAgentControlStatusToHttpError_OnUnknownException()
    {
        // Arrange
        Mock.Arrange(() => _dataRequestWire.SendData(Arg.IsAny<string>(), Arg.IsAny<ConnectionInfo>(), Arg.IsAny<string>(), Arg.IsAny<Guid>()))
            .Throws(new Exception("Unknown exception"));

        // Act & Assert
        var ex = Assert.Throws<Exception>(() => _connectionHandler.SendDataRequest<object>("testMethod"));
        Assert.That(ex.Message, Is.EqualTo("Unknown exception"));

        Mock.Assert(() => _agentHealthReporter.SetAgentControlStatus(HealthCodes.HttpError, "unknown", "testMethod"), Occurs.Once());
    }
}
