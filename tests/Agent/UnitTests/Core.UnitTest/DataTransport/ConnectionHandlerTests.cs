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
using Newtonsoft.Json;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DataTransport.Tests
{
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
                _dataRequestWire
            );

            _connectionHandler.OverrideConfigForTesting(_configuration);
        }

        [TearDown]
        public void TearDown()
        {
            _connectionHandler.Dispose();
            _labelsService.Dispose();
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
}
