using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
	public class AspNetCoreMvcBasicRequestsTests : IClassFixture<RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture>
	{
		[NotNull]
		private readonly RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture _fixture;

		public AspNetCoreMvcBasicRequestsTests([NotNull] RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture fixture, [NotNull] ITestOutputHelper output)
		{
			_fixture = fixture;
			_fixture.TestLogger = output;
			_fixture.Actions
			(
				setupConfiguration: () =>
				{
					var configPath = fixture.DestinationNewRelicConfigFilePath;
					var configModifier = new NewRelicConfigModifier(configPath);
					configModifier.ForceTransactionTraces();
				},
				exerciseApplication: () =>
				{
					_fixture.Get();
				}
			);
			_fixture.Initialize();
		}

		[Fact]
		public void Test()
		{
			var expectedMetrics = new List<Assertions.ExpectedMetric>
			{
				new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", callCount = 1 },
				new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsCollected", callCount = 1 },
				new Assertions.ExpectedMetric { metricName = @"Supportability/OS/Linux", callCount = 1 },
				new Assertions.ExpectedMetric { metricName = @"Apdex"},
				new Assertions.ExpectedMetric { metricName = @"ApdexAll"},
				new Assertions.ExpectedMetric { metricName = @"Apdex/MVC/Home/Index"},
				new Assertions.ExpectedMetric { metricName = @"WebTransaction", callCount = 1 },
				new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", callCount = 1 },
				new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/Home/Index", callCount = 1 },
				new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", callCount = 1 },
				new Assertions.ExpectedMetric { metricName = @"WebTransaction", callCount = 1 },
				new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", callCount = 1 },
				new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/MVC/Home/Index", callCount = 1 },
				new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", callCount = 1 },
				new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", metricScope = @"WebTransaction/MVC/Home/Index", callCount = 1 },
				new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", callCount = 1 },
				new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", metricScope = @"WebTransaction/MVC/Home/Index", callCount = 1 },
			};

			var expectedTransactionTraceSegments = new List<String>
			{
				@"Middleware Pipeline",
				@"DotNet/HomeController/Index"
			};

			var metrics = _fixture.AgentLog.GetMetrics().ToList();

			var transactionSample = _fixture.AgentLog.GetTransactionSamples().Where(sample => sample.Path == @"WebTransaction/MVC/Home/Index")
				.FirstOrDefault();

			Assert.NotNull(metrics);

			Assert.NotNull(transactionSample);

			NrAssert.Multiple
			(
				() => Assertions.MetricsExist(expectedMetrics, metrics),
				() => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
			);
		}
	}
}
