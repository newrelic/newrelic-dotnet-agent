using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
	public class AsyncWcfService : IClassFixture<RemoteServiceFixtures.AsyncWcfService>
	{
		[NotNull]
		private readonly RemoteServiceFixtures.AsyncWcfService _fixture;

		public AsyncWcfService([NotNull] RemoteServiceFixtures.AsyncWcfService fixture, [NotNull] ITestOutputHelper output)
		{
			_fixture = fixture;
			_fixture.TestLogger = output;
			_fixture.Actions(
				setupConfiguration: () =>
				{
					var configPath = fixture.DestinationNewRelicConfigFilePath;
					var configModifier = new NewRelicConfigModifier(configPath);
					configModifier.ForceTransactionTraces();
					
					CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(configPath, new[] {"configuration", "attributes"}, "include", "service.request.*");
					CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(configPath, new[] {"configuration", "attributes"}, "exclude", "service.request.otherValue");
				});
			_fixture.Initialize();
		}

		[Fact]
		public void Test()
		{
			var expectedMetrics = new List<Assertions.ExpectedMetric>
			{
				new Assertions.ExpectedMetric {metricName = @"HttpDispatcher", callCount = 2},
				new Assertions.ExpectedMetric {metricName = @"External/all", callCount = 1},
				new Assertions.ExpectedMetric {metricName = @"External/allWeb", callCount = 1},
				new Assertions.ExpectedMetric {metricName = @"External/www.google.com/all", callCount = 1},
				new Assertions.ExpectedMetric {metricName = @"External/www.google.com/Stream/GET", callCount = 1},
				new Assertions.ExpectedMetric {metricName = @"External/www.google.com/Stream/GET", metricScope = @"WebTransaction/WCF/NewRelic.Agent.IntegrationTests.Applications.AsyncWcfService.IWcfService.BeginServiceMethod", callCount = 1},
				new Assertions.ExpectedMetric {metricName = @"WebTransaction", callCount = 2},
				new Assertions.ExpectedMetric {metricName = @"WebTransaction/WCF/NewRelic.Agent.IntegrationTests.Applications.AsyncWcfService.IWcfService.BeginServiceMethod", callCount = 1},
				new Assertions.ExpectedMetric {metricName = @"WebTransaction/WCF/NewRelic.Agent.IntegrationTests.Applications.AsyncWcfService.IWcfService.EndServiceMethod", callCount = 1},
				new Assertions.ExpectedMetric {metricName = @"ApdexAll"},
				new Assertions.ExpectedMetric {metricName = @"Apdex"},
				new Assertions.ExpectedMetric {metricName = @"Apdex/WCF/NewRelic.Agent.IntegrationTests.Applications.AsyncWcfService.IWcfService.BeginServiceMethod"},
				new Assertions.ExpectedMetric {metricName = @"Apdex/WCF/NewRelic.Agent.IntegrationTests.Applications.AsyncWcfService.IWcfService.EndServiceMethod"},
				new Assertions.ExpectedMetric {metricName = @"DotNet/NewRelic.Agent.IntegrationTests.Applications.AsyncWcfService.IWcfService.BeginServiceMethod", metricScope = @"WebTransaction/WCF/NewRelic.Agent.IntegrationTests.Applications.AsyncWcfService.IWcfService.BeginServiceMethod"},
				new Assertions.ExpectedMetric {metricName = @"DotNet/NewRelic.Agent.IntegrationTests.Applications.AsyncWcfService.IWcfService.EndServiceMethod", metricScope = @"WebTransaction/WCF/NewRelic.Agent.IntegrationTests.Applications.AsyncWcfService.IWcfService.EndServiceMethod"},
			};
			var unexpectedMetrics = new List<Assertions.ExpectedMetric>
			{
				new Assertions.ExpectedMetric {metricName = @"External/allOther"},
				new Assertions.ExpectedMetric {metricName = @"OtherTransaction/all"}
			};

			var expectedTraceSegmentNames = new List<String>
			{
				@"NewRelic.Agent.IntegrationTests.Applications.AsyncWcfService.IWcfService.BeginServiceMethod",
				@"External/www.google.com/Stream/GET"
			};
			var expectedSegmentParameters = new List<Assertions.ExpectedSegmentParameter>
			{
				new Assertions.ExpectedSegmentParameter { segmentName = @"External/www.google.com/Stream/GET", parameterName = @"uri", parameterValue = @"https://www.google.com:443/" },
			};
			var expectedTraceAttributes = new Dictionary<String, String>
			{
				{"service.request.value", "foo"},
			};
			var unexpectedTraceAttributes = new List<String>
			{
				"service.request.otherValue",
			};

			var metrics = _fixture.AgentLog.GetMetrics().ToList();

			var transactionSample = _fixture.AgentLog.GetTransactionSamples()
				.Where(sample => sample.Path == _fixture.ExpectedTransactionName)
				.FirstOrDefault();

			NrAssert.Multiple(
				() => Assertions.MetricsExist(expectedMetrics, metrics),
				() => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
				() => Assert.NotNull(transactionSample),
				() => Assertions.TransactionTraceHasAttributes(expectedTraceAttributes, TransactionTraceAttributeType.Agent, transactionSample),
				() => Assertions.TransactionTraceDoesNotHaveAttributes(unexpectedTraceAttributes, TransactionTraceAttributeType.Agent, transactionSample),
				() => Assertions.TransactionTraceSegmentsExist(expectedTraceSegmentNames, transactionSample),
				() => Assertions.TransactionTraceSegmentParametersExist(expectedSegmentParameters, transactionSample),
				() => Assert.Empty(_fixture.AgentLog.GetErrorTraces()),
				() => Assert.Empty(_fixture.AgentLog.GetErrorEvents())
				);
		}
	}
}
