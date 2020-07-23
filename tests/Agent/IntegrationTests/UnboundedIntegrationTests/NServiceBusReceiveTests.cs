using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;
using Assert = Xunit.Assert;

namespace NewRelic.Agent.UnboundedIntegrationTests
{
	public class NServiceBusReceiveTests : IClassFixture<NServiceBusReceiverFixture>
	{
		[NotNull] private readonly NServiceBusReceiverFixture _fixture;

		public NServiceBusReceiveTests([NotNull] NServiceBusReceiverFixture fixture, [NotNull] ITestOutputHelper output)
		{
			_fixture = fixture;
			_fixture.TestLogger = output;
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
					// TODO: long term seems we could make these calls from the fixture.
					_fixture.SendFixture.GetMessageQueue_NServiceBus_SendValid();
					_fixture.SendFixture.GetMessageQueue_NServiceBus_SendInvalid();

					_fixture.AgentLog.WaitForLogLine(AgentLogFile.ErrorTraceDataLogLineRegex, TimeSpan.FromMinutes(2));
					_fixture.AgentLog.WaitForLogLine(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(1));
				}
			);
			_fixture.Initialize();
		}

		[Fact]
		public void Test()
		{
			var expectedMetrics = new List<Assertions.ExpectedMetric>
			{
				new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Consume/Named/NServiceBusReceiver.SampleNServiceBusMessage2"},
				new Assertions.ExpectedMetric { metricName = @"MessageBroker/NServiceBus/Queue/Consume/Named/NServiceBusReceiver.SampleNServiceBusMessage2",
												metricScope = @"OtherTransaction/Message/NServiceBus/Queue/Named/NServiceBusReceiver.SampleNServiceBusMessage2"},
				new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Message/NServiceBus/Queue/Named/NServiceBusReceiver.SampleNServiceBusMessage2"},
				new Assertions.ExpectedMetric { metricName = @"OtherTransactionTotalTime/Message/NServiceBus/Queue/Named/NServiceBusReceiver.SampleNServiceBusMessage2"}
			};

			var expectedTransactionTraceSegments = new List<String>
			{
				@"MessageBroker/NServiceBus/Queue/Consume/Named/NServiceBusReceiver.SampleNServiceBusMessage2"
			};

			var metrics = _fixture.AgentLog.GetMetrics().ToList();

			var transactionSample = _fixture.AgentLog.TryGetTransactionSample("OtherTransaction/Message/NServiceBus/Queue/Named/NServiceBusReceiver.SampleNServiceBusMessage2");
			var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("OtherTransaction/Message/NServiceBus/Queue/Named/NServiceBusReceiver.SampleNServiceBusMessage2");
			var errorTrace =
				_fixture.AgentLog.TryGetErrorTrace(
					"OtherTransaction/Message/NServiceBus/Queue/Named/NServiceBusReceiver.SampleNServiceBusMessage2");

			NrAssert.Multiple(
				() => Assert.NotNull(transactionSample),
				() => Assert.NotNull(transactionEvent),
				() => Assert.NotNull(errorTrace)
			);

			NrAssert.Multiple
			(
				() => Assertions.MetricsExist(expectedMetrics, metrics),
				() => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
			);
		}
	}
}
