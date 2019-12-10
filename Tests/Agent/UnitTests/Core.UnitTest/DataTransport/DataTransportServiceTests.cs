using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
	[TestFixture]
	public class DataTransportServiceTests
	{
		private DataTransportService _dataTransportService;
		private IConnectionManager _connectionManager;
		private IConfiguration _configuration;
		private DisposableCollection _disposableCollection;
		private IAgentHealthReporter _agentHealthReporter;
		private IConnectionHandler _connectionHandler;

		private static readonly Exception[] ExceptionsThatShouldTriggerSupportabilityMetrics =
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
			var scheduler = Mock.Create<IScheduler>();
			_connectionManager = Mock.Create(() => new ConnectionManager(_connectionHandler, scheduler));
			var dateTimeStatic = Mock.Create<IDateTimeStatic>();
			_agentHealthReporter = Mock.Create<IAgentHealthReporter>(); 
			_disposableCollection.Add(_dataTransportService = new DataTransportService(_connectionManager, dateTimeStatic, _agentHealthReporter));
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
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Returns<string, object[]>(null);

			var result = _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Enumerable.Empty<TransactionEventWireModel>());

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
		public void SendXyz_ReturnsCorrectRetention_IfHttpException(HttpStatusCode statusCode, DataTransportResponseStatus expected)
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(new HttpException(statusCode, null));

			var actual = _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Enumerable.Empty<TransactionEventWireModel>());

			Assert.AreEqual(expected, actual);
		}

		[Test]
		public void SendXyz_ReturnsCommunicationError_IfSocketException()
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(new SocketException(-1));

			var result = _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Enumerable.Empty<TransactionEventWireModel>());

			Assert.AreEqual(DataTransportResponseStatus.Retain, result);
		}

		[Test]
		public void SendXyz_ReturnsCommunicationError_IfWebException()
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(new WebException());

			var result = _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Enumerable.Empty<TransactionEventWireModel>());

			Assert.AreEqual(DataTransportResponseStatus.Retain, result);
		}

		[Test]
		public void SendXyz_ReturnsCorrectRetention_IfOperationCanceledException()
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(new OperationCanceledException());

			var result = _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Enumerable.Empty<TransactionEventWireModel>());

			Assert.AreEqual(DataTransportResponseStatus.Retain, result);
		}

		[Test]
		public void SendXyz_ReturnsOtherError_IfOtherException()
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(new Exception());

			var result = _dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Enumerable.Empty<TransactionEventWireModel>());

			Assert.AreEqual(DataTransportResponseStatus.Discard, result);
		}

		[TestCase(HttpStatusCode.Unauthorized)]
		[TestCase(HttpStatusCode.Conflict)]
		public void SendXyz_PublishesRestartAgentEvent_ForCertainHttpStatusCodes(HttpStatusCode statusCode)
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(new HttpException(statusCode, null));

			using (new EventExpectation<RestartAgentEvent>())
			{
				_dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Enumerable.Empty<TransactionEventWireModel>());
			}

			Mock.Assert(() => _connectionHandler.Disconnect(), Occurs.Once());
			Mock.Assert(() => _connectionHandler.Connect(), Occurs.Once());
		}

		[Test]
		public void SendXyz_PublishesShutdownAgentEvent_IfForHttpStatusCodeGone()
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(new HttpException(HttpStatusCode.Gone, null));

			using (new EventExpectation<KillAgentEvent>())
			{
				_dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Enumerable.Empty<TransactionEventWireModel>());
			}
		}

		[Test]
		public void SendXyz_GenerateCollectorErrorExceptionSupportabilityMetrics_ForHttpExceptions()
		{
			var exception = new HttpException(HttpStatusCode.InternalServerError, "Internal Server Error");

			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(exception);

			_dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Enumerable.Empty<TransactionEventWireModel>());

			Mock.Assert(() => _agentHealthReporter.ReportSupportabilityCollectorErrorException(Arg.Is("analytic_event_data"), Arg.IsAny<TimeSpan>(), Arg.Is(exception.StatusCode)));
		}

		[Test, TestCaseSource(nameof(ExceptionsThatShouldTriggerSupportabilityMetrics))]
		public void SendXyz_GenerateCollectorErrorExceptionSupportabilityMetrics_ForExceptions(Exception exception)
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(exception);

			_dataTransportService.Send(Arg.IsAny<EventHarvestData>(), Enumerable.Empty<TransactionEventWireModel>());

			Mock.Assert(() => _agentHealthReporter.ReportSupportabilityCollectorErrorException(Arg.Is("analytic_event_data"), Arg.IsAny<TimeSpan>(), Arg.IsNull<HttpStatusCode?>()));
		}
	}
}
