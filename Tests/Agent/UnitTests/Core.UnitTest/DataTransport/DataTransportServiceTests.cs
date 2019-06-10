using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Exceptions;
using NewRelic.Agent.Core.Fixtures;
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

		private static readonly HttpException[] HttpExceptionsThatShouldTriggerSupportabilityMetrics =
		{
			new ServerErrorException("Server Error Exception", HttpStatusCode.InternalServerError),
			new RequestTimeoutException("Request timeout exception"),
			new PostTooLargeException("Post too large")
		};

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

			_connectionManager = Mock.Create<IConnectionManager>();
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

			var result = _dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());

			Assert.AreEqual(DataTransportResponseStatus.RequestSuccessful, result);
		}

		[Test]
		public void SendXyz_ReturnsConnectionError_IfConnectionException()
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(new ConnectionException(null));

			var result = _dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());

			Assert.AreEqual(DataTransportResponseStatus.ConnectionError, result);
		}

		[Test]
		public void SendXyz_ReturnsPostTooBigError_IfPostTooBigException()
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(new PostTooBigException(null));

			var result = _dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());

			Assert.AreEqual(DataTransportResponseStatus.PostTooBigError, result);
		}

		[Test]
		public void SendXyz_ReturnsPostTooBigError_IfPostTooLargeException()
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(new PostTooLargeException(null));

			var result = _dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());

			Assert.AreEqual(DataTransportResponseStatus.PostTooBigError, result);
		}

		[Test]
		public void SendXyz_ReturnsServerError_IfServerException()
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(new ServerErrorException(null, HttpStatusCode.InternalServerError));

			var result = _dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());

			Assert.AreEqual(DataTransportResponseStatus.ServerError, result);
		}

		[Test]
		public void SendXyz_ReturnsRequestTimeout_IfRequestTimeoutException()
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(new RequestTimeoutException(null));

			var result = _dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());

			Assert.AreEqual(DataTransportResponseStatus.RequestTimeout, result);
		}

		[Test]
		public void SendXyz_ReturnsCommunicationError_IfSocketException()
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(new SocketException(-1));

			var result = _dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());

			Assert.AreEqual(DataTransportResponseStatus.CommunicationError, result);
		}

		[Test]
		public void SendXyz_ReturnsCommunicationError_IfWebException()
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(new WebException());

			var result = _dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());

			Assert.AreEqual(DataTransportResponseStatus.CommunicationError, result);
		}

		[Test]
		public void SendXyz_ReturnsOtherError_IfOtherException()
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(new Exception());

			var result = _dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());

			Assert.AreEqual(DataTransportResponseStatus.OtherError, result);
		}

		[Test]
		public void SendXyz_PublishesRestartAgentEvent_IfForceRestartException()
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(new ForceRestartException(null));

			using (new EventExpectation<RestartAgentEvent>())
			{
				_dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());
			}
		}

		[Test]
		public void SendXyz_PublishesShutdownAgentEvent_IfLicenseException()
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(new LicenseException(null));

			using (new EventExpectation<KillAgentEvent>())
			{
				_dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());
			}
		}

		[Test]
		public void SendXyz_PublishesShutdownAgentEvent_IfForceDisconnectException()
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(new ForceDisconnectException(null));

			using (new EventExpectation<KillAgentEvent>())
			{
				_dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());
			}
		}

		[Test, TestCaseSource(nameof(HttpExceptionsThatShouldTriggerSupportabilityMetrics))]
		public void SendXyz_GenerateCollectorErrorExceptionSupportabilityMetrics_ForHttpExceptions(HttpException exception)
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(exception);

			_dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());

			Mock.Assert(() => _agentHealthReporter.ReportSupportabilityCollectorErrorException(Arg.Is("analytic_event_data"), Arg.IsAny<TimeSpan>(), Arg.Is(exception.StatusCode)));
		}

		[Test, TestCaseSource(nameof(ExceptionsThatShouldTriggerSupportabilityMetrics))]
		public void SendXyz_GenerateCollectorErrorExceptionSupportabilityMetrics_ForExceptions(Exception exception)
		{
			Mock.Arrange(() => _connectionManager.SendDataRequest<object>(Arg.IsAny<string>(), Arg.IsAny<object[]>()))
				.Throws(exception);

			_dataTransportService.Send(Enumerable.Empty<TransactionEventWireModel>());

			Mock.Assert(() => _agentHealthReporter.ReportSupportabilityCollectorErrorException(Arg.Is("analytic_event_data"), Arg.IsAny<TimeSpan>(), Arg.IsNull<HttpStatusCode?>()));
		}
	}
}
