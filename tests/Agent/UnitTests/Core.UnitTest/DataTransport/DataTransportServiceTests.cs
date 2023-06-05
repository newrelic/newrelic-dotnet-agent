// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Exceptions;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DataTransport
{

    public class SendTransactionEventDataTransportServiceTests : DataTransportServiceTestBase
    {
        public override async Task<DataTransportResponseStatus> ExecuteRequestAsync(DataTransportService service)
        {
            return await service.SendAsync(Arg.IsAny<EventHarvestData>(), Enumerable.Empty<TransactionEventWireModel>());
        }

        public override string GetExpectedDestinationAreaName()
        {
            return "analytic_event_data";
        }
    }

    public class SendLogEventDataTransportServiceTests : DataTransportServiceTestBase
    {
        public override async Task<DataTransportResponseStatus> ExecuteRequestAsync(DataTransportService service)
        {
            return await service.SendAsync(Arg.IsAny<LogEventWireModelCollection>());
        }

        public override string GetExpectedDestinationAreaName()
        {
            return "log_event_data";
        }
    }

    [TestFixture]
    public abstract class DataTransportServiceTestBase
    {
        public abstract Task<DataTransportResponseStatus> ExecuteRequestAsync(DataTransportService service);
        public abstract string GetExpectedDestinationAreaName();

        private DataTransportService _dataTransportService;
        private IConnectionManager _connectionManager;
        private IConfiguration _configuration;
        private DisposableCollection _disposableCollection;
        private IAgentHealthReporter _agentHealthReporter;
        private IConnectionHandler _connectionHandler;
        private IScheduler _scheduler;
        private IDateTimeStatic _dateTimeStatic;

        public static readonly Exception[] ExceptionsThatShouldTriggerSupportabilityMetrics =
        {
            new Exception("plain exception"),
            new SocketException(-1),
            new WebException()
        };

        [SetUp]
        public void SetUp()
        {
            _disposableCollection = new DisposableCollection();

            _configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _configuration.AgentRunId).Returns("MyAgentRunId");
            _disposableCollection.Add(new ConfigurationAutoResponder(_configuration));

            _connectionHandler = Mock.Create<IConnectionHandler>();
            _scheduler = Mock.Create<IScheduler>();
            _connectionManager = Mock.Create<IConnectionManager>();
            _dateTimeStatic = Mock.Create<IDateTimeStatic>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            _disposableCollection.Add(_dataTransportService = new DataTransportService(_connectionManager, _dateTimeStatic, _agentHealthReporter));
        }

        [TearDown]
        public void TearDown()
        {
            _disposableCollection.Dispose();
        }

        [Test]
        public async Task SendXyz_ReturnsSuccessful_IfRequestSuccessful()
        {
            Mock.Arrange(() => _connectionManager.SendDataRequestAsync<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
                .ReturnsAsync(null);

            var result = await ExecuteRequestAsync(_dataTransportService);

            Assert.AreEqual(DataTransportResponseStatus.RequestSuccessful, result);
        }

        [TestCase((HttpStatusCode)400, DataTransportResponseStatus.Discard)]
        [TestCase((HttpStatusCode)401, DataTransportResponseStatus.Discard)]
        [TestCase((HttpStatusCode)403, DataTransportResponseStatus.Discard)]
        [TestCase((HttpStatusCode)404, DataTransportResponseStatus.Discard)]
        [TestCase((HttpStatusCode)405, DataTransportResponseStatus.Discard)]
        [TestCase((HttpStatusCode)407, DataTransportResponseStatus.Discard)]
        [TestCase((HttpStatusCode)408, DataTransportResponseStatus.Retain)]
        [TestCase((HttpStatusCode)409, DataTransportResponseStatus.Discard)]
        [TestCase((HttpStatusCode)410, DataTransportResponseStatus.Discard)]
        [TestCase((HttpStatusCode)411, DataTransportResponseStatus.Discard)]
        [TestCase((HttpStatusCode)413, DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard)]
        [TestCase((HttpStatusCode)414, DataTransportResponseStatus.Discard)]
        [TestCase((HttpStatusCode)415, DataTransportResponseStatus.Discard)]
        [TestCase((HttpStatusCode)417, DataTransportResponseStatus.Discard)]
        [TestCase((HttpStatusCode)429, DataTransportResponseStatus.Retain)]
        [TestCase((HttpStatusCode)431, DataTransportResponseStatus.Discard)]
        [TestCase((HttpStatusCode)500, DataTransportResponseStatus.Retain)]
        [TestCase((HttpStatusCode)503, DataTransportResponseStatus.Retain)]
        [TestCase((HttpStatusCode)333, DataTransportResponseStatus.Discard)]
        [TestCase((HttpStatusCode)444, DataTransportResponseStatus.Discard)]
        [TestCase((HttpStatusCode)555, DataTransportResponseStatus.Discard)]
        public async Task SendXyz_ReturnsCorrectRetention_IfHttpException(HttpStatusCode statusCode, DataTransportResponseStatus expected)
        {
            Mock.Arrange(() => _connectionManager.SendDataRequestAsync<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
                .Throws(new HttpException(statusCode, null));

            var actual = await ExecuteRequestAsync(_dataTransportService);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public async Task SendXyz_ReturnsCommunicationError_IfSocketException()
        {
            Mock.Arrange(() => _connectionManager.SendDataRequestAsync<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
                .Throws(new SocketException(-1));

            var result = await ExecuteRequestAsync(_dataTransportService);

            Assert.AreEqual(DataTransportResponseStatus.Retain, result);
        }

        [Test]
        public async Task SendXyz_ReturnsCommunicationError_IfWebException()
        {
            Mock.Arrange(() => _connectionManager.SendDataRequestAsync<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
                .Throws(new WebException());

            var result = await ExecuteRequestAsync(_dataTransportService);

            Assert.AreEqual(DataTransportResponseStatus.Retain, result);
        }

        [Test]
        public async Task SendXyz_ReturnsCorrectRetention_IfOperationCanceledException()
        {
            Mock.Arrange(() => _connectionManager.SendDataRequestAsync<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
                .Throws(new OperationCanceledException());

            var result = await ExecuteRequestAsync(_dataTransportService);

            Assert.AreEqual(DataTransportResponseStatus.Retain, result);
        }

        [Test]
        public async Task SendXyz_ReturnsOtherError_IfOtherException()
        {
            Mock.Arrange(() => _connectionManager.SendDataRequestAsync<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
                .Throws(new Exception());

            var result = await ExecuteRequestAsync(_dataTransportService);

            Assert.AreEqual(DataTransportResponseStatus.Discard, result);
        }

        [TestCase(HttpStatusCode.Unauthorized)]
        [TestCase(HttpStatusCode.Conflict)]
        public async Task SendXyz_PublishesRestartAgentEvent_ForCertainHttpStatusCodes(HttpStatusCode statusCode)
        {
            Mock.Arrange(() => _connectionManager.SendDataRequestAsync<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
                .Throws(new HttpException(statusCode, null));

            using (new EventExpectation<RestartAgentEvent>())
            {
                await ExecuteRequestAsync(_dataTransportService);
            }
        }

        [TestCase(HttpStatusCode.Unauthorized)]
        [TestCase(HttpStatusCode.Conflict)]
        public async Task SendXyz_ConnectionHandler_DisconnectAndConnectAreCalled_ForCertainHttpStatusCodes(HttpStatusCode statusCode)
        {
            _connectionManager = new ConnectionManager(_connectionHandler, _scheduler);
            _disposableCollection.Add(_dataTransportService = new DataTransportService(_connectionManager, _dateTimeStatic, _agentHealthReporter));

            Mock.Arrange(() => _connectionHandler.SendDataRequestAsync<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
                .Throws(new HttpException(statusCode, null));

            await ExecuteRequestAsync(_dataTransportService);

            Mock.Assert(() => _connectionHandler.DisconnectAsync(), Occurs.Once());
            Mock.Assert(() => _connectionHandler.ConnectAsync(), Occurs.Once());
        }

        [Test]
        public async Task SendXyz_PublishesShutdownAgentEvent_IfForHttpStatusCodeGone()
        {
            Mock.Arrange(() => _connectionManager.SendDataRequestAsync<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
                .Throws(new HttpException(HttpStatusCode.Gone, null));

            using (new EventExpectation<KillAgentEvent>())
            {
                await ExecuteRequestAsync(_dataTransportService);
            }
        }

        [Test]
        public async Task SendXyz_GenerateCollectorErrorExceptionSupportabilityMetrics_ForHttpExceptions()
        {
            var exception = new HttpException(HttpStatusCode.InternalServerError, "Internal Server Error");

            Mock.Arrange(() => _connectionManager.SendDataRequestAsync<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
                .Throws(exception);

            await ExecuteRequestAsync(_dataTransportService);

            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityCollectorErrorException(Arg.Is(GetExpectedDestinationAreaName()), Arg.IsAny<TimeSpan>(), Arg.Is(exception.StatusCode)));
        }

        [Test, TestCaseSource(nameof(ExceptionsThatShouldTriggerSupportabilityMetrics))]
        public async Task SendXyz_GenerateCollectorErrorExceptionSupportabilityMetrics_ForExceptions(Exception exception)
        {
            Mock.Arrange(() => _connectionManager.SendDataRequestAsync<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
                .Throws(exception);

            await ExecuteRequestAsync(_dataTransportService);

            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityCollectorErrorException(Arg.Is(GetExpectedDestinationAreaName()), Arg.IsAny<TimeSpan>(), Arg.IsNull<HttpStatusCode?>()));
        }
    }
}
