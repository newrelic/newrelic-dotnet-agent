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
	public class CustomInstrumentationAsync : IClassFixture<RemoteServiceFixtures.BasicMvcApplication>
	{
		[NotNull]
		private readonly RemoteServiceFixtures.BasicMvcApplication _fixture;

		public CustomInstrumentationAsync([NotNull] RemoteServiceFixtures.BasicMvcApplication fixture, [NotNull] ITestOutputHelper output)
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
					
					var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\CustomInstrumentationAsync.xml";

					CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationAsyncController", "CustomMethodDefaultWrapperAsync", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "MyCustomMetricName", 7);
					CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationAsyncController", "CustomSegmentTransactionSegmentWrapper", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.CustomSegmentWrapperAsync");
					CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicMvcApplication", "BasicMvcApplication.Controllers.CustomInstrumentationAsyncController", "CustomSegmentAlternateParameterNamingTheSegment", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.CustomSegmentWrapperAsync");
				},
				exerciseApplication: () =>
				{
					_fixture.GetCustomInstrumentationAsync();
				}
			);
			_fixture.Initialize();
		}

		[Fact]
		public void Test()
		{
			var expectedMetrics = new List<Assertions.ExpectedMetric>
			{
				new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomMetricName", callCount = 1 },
				new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomMetricName", callCount = 1 },
				new Assertions.ExpectedMetric { metricName = @"Custom/AsyncCustomSegmentName", callCount = 1 },
				new Assertions.ExpectedMetric { metricName = @"Custom/AsyncCustomSegmentNameAlternate", callCount = 1 },

				// Scoped
				new Assertions.ExpectedMetric { metricName = @"Custom/AsyncCustomSegmentName", metricScope = "WebTransaction/Custom/MyCustomMetricName", callCount = 1 },
				new Assertions.ExpectedMetric { metricName = @"Custom/AsyncCustomSegmentNameAlternate", metricScope = "WebTransaction/Custom/MyCustomMetricName", callCount = 1 }
			};

			var expectedTransactionTraceSegments = new List<string>
			{
				@"MyCustomMetricName",
				@"AsyncCustomSegmentName",
				@"AsyncCustomSegmentNameAlternate"
			};

			var metrics = _fixture.AgentLog.GetMetrics().ToList();

			var transactionSample = _fixture.AgentLog
				.GetTransactionSamples()
				.FirstOrDefault(sample => sample.Path == @"WebTransaction/Custom/MyCustomMetricName");

			var transactionEvent = _fixture.AgentLog.GetTransactionEvents()
				.FirstOrDefault();

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
