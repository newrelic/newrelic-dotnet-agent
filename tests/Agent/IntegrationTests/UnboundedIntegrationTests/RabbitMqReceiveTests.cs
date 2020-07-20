using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests
{
	public class RabbitMqReceiveTests : IClassFixture<RemoteServiceFixtures.RabbitMqReceiverFixture>
	{
		[NotNull]
		private readonly RemoteServiceFixtures.RabbitMqReceiverFixture _fixture;

		public RabbitMqReceiveTests([NotNull] RemoteServiceFixtures.RabbitMqReceiverFixture fixture, [NotNull] ITestOutputHelper output)
		{
			_fixture = fixture;
			_fixture.TestLogger = output;
			_fixture.CommandLineArguments = $"--queue={_fixture.QueueName}";

			_fixture.Actions
			(
				setupConfiguration: () =>
				{
					var configPath = _fixture.DestinationNewRelicConfigFilePath;
					var configModifier = new NewRelicConfigModifier(configPath);

					configModifier.ForceTransactionTraces();
				},
				exerciseApplication: () =>
				{
					_fixture.CreateQueueAndSendMessage();
					_fixture.AgentLog.WaitForLogLine(AgentLogFile.HarvestFinishedLogLineRegex, TimeSpan.FromMinutes(2));
				}
			);

			_fixture.Initialize();
		}

		[Fact]
		public void Test()
		{
			var metrics = _fixture.AgentLog.GetMetrics().ToList();
			var expectedMetrics = new List<Assertions.ExpectedMetric>
			{
				//TODO: DOTNET-2171: OtherTransaction/all should be generated but there is a bug in the agent w/ message broker transactions always being "web". 
				//new Assertions.ExpectedMetric { metricName = "OtherTransaction/all", callCount = 1}, 
				new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Consume/Named/{_fixture.QueueName}", callCount = 1},
				new Assertions.ExpectedMetric { metricName = $"MessageBroker/RabbitMQ/Queue/Consume/Named/{_fixture.QueueName}", callCount = 1, metricScope = $"OtherTransaction/Message/RabbitMQ/Queue/Named/{_fixture.QueueName}"},
			};

			var expectedTransactionTraceSegments = new List<String>
			{
				$"MessageBroker/RabbitMQ/Queue/Consume/Named/{_fixture.QueueName}"
			};

			var transactionSample = _fixture.AgentLog.TryGetTransactionSample($"OtherTransaction/Message/RabbitMQ/Queue/Named/{_fixture.QueueName}");
			var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"OtherTransaction/Message/RabbitMQ/Queue/Named/{_fixture.QueueName}");

			NrAssert.Multiple(
				() => Assert.NotNull(transactionSample),
				() => Assert.NotNull(transactionEvent)
			);

			NrAssert.Multiple
			(
				() => Assertions.MetricsExist(expectedMetrics, metrics),
				() => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
			);
		}
	}
}
