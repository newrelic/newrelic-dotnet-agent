using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Exceptions;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.SystemInterfaces;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DataTransport
{
    [TestFixture]
    public class DataTransportServiceTests
    {
        [NotNull]
        private DataTransportService _dataTransportService;

        [NotNull]
        private IConnectionManager _connectionManager;

        [NotNull]
        private IConfiguration _configuration;

        [NotNull]
        private DisposableCollection _disposableCollection;

        [SetUp]
        public void SetUp()
        {
            _disposableCollection = new DisposableCollection();

            _configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _configuration.AgentRunId).Returns("MyAgentRunId");
            _disposableCollection.Add(new ConfigurationAutoResponder(_configuration));

            _connectionManager = Mock.Create<IConnectionManager>();
            var dateTimeStatic = Mock.Create<IDateTimeStatic>();
            _disposableCollection.Add(_dataTransportService = new DataTransportService(_connectionManager, dateTimeStatic));
        }

        [TearDown]
        public void TearDown()
        {
            _disposableCollection.Dispose();
        }

        // Technically these tests should be run against every public API method instead of just SendTransactionEventWireModels, but writing all those tests would be a big pain in the ass

        [Test]
        public void SendXyz_ReturnsSuccessful_IfRequestSuccessful()
        {
            Mock.Arrange(() => _connectionManager.SendDataRequest<Object>(Arg.IsAny<String>(), Arg.IsAny<Object[]>()))
                .Returns<String, Object[]>(null);

            var result = _dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());

            Assert.AreEqual(DataTransportResponseStatus.RequestSuccessful, result);
        }

        [Test]
        public void SendXyz_ReturnsConnectionError_IfConnectionException()
        {
            Mock.Arrange(() => _connectionManager.SendDataRequest<Object>(Arg.IsAny<String>(), Arg.IsAny<Object[]>()))
                .Throws(new ConnectionException(null));

            var result = _dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());

            Assert.AreEqual(DataTransportResponseStatus.ConnectionError, result);
        }

        [Test]
        public void SendXyz_ReturnsPostTooBigError_IfPostTooBigException()
        {
            Mock.Arrange(() => _connectionManager.SendDataRequest<Object>(Arg.IsAny<String>(), Arg.IsAny<Object[]>()))
                .Throws(new PostTooBigException(null));

            var result = _dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());

            Assert.AreEqual(DataTransportResponseStatus.PostTooBigError, result);
        }

        [Test]
        public void SendXyz_ReturnsServiceUnavailableError_IfServiceUnavailableException()
        {
            Mock.Arrange(() => _connectionManager.SendDataRequest<Object>(Arg.IsAny<String>(), Arg.IsAny<Object[]>()))
                .Throws(new ServiceUnavailableException(null));

            var result = _dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());

            Assert.AreEqual(DataTransportResponseStatus.ServiceUnavailableError, result);
        }

        [Test]
        public void SendXyz_ReturnsOtherError_IfOtherException()
        {
            Mock.Arrange(() => _connectionManager.SendDataRequest<Object>(Arg.IsAny<String>(), Arg.IsAny<Object[]>()))
                .Throws(new Exception());

            var result = _dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());

            Assert.AreEqual(DataTransportResponseStatus.OtherError, result);
        }

        [Test]
        public void SendXyz_PublishesRestartAgentEvent_IfForceRestartException()
        {
            Mock.Arrange(() => _connectionManager.SendDataRequest<Object>(Arg.IsAny<String>(), Arg.IsAny<Object[]>()))
                .Throws(new ForceRestartException(null));

            using (new EventExpectation<RestartAgentEvent>())
            {
                _dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());
            }
        }

        [Test]
        public void SendXyz_PublishesShutdownAgentEvent_IfLicenseException()
        {
            Mock.Arrange(() => _connectionManager.SendDataRequest<Object>(Arg.IsAny<String>(), Arg.IsAny<Object[]>()))
                .Throws(new LicenseException(null));

            using (new EventExpectation<KillAgentEvent>())
            {
                _dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());
            }
        }

        [Test]
        public void SendXyz_PublishesShutdownAgentEvent_IfForceDisconnectException()
        {
            Mock.Arrange(() => _connectionManager.SendDataRequest<Object>(Arg.IsAny<String>(), Arg.IsAny<Object[]>()))
                .Throws(new ForceDisconnectException(null));

            using (new EventExpectation<KillAgentEvent>())
            {
                _dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());
            }
        }
    }
}
