using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
	public class MemoryMvc : IClassFixture<RemoteServiceFixtures.BasicMvcApplication>
	{
		[NotNull]
		private readonly RemoteServiceFixtures.BasicMvcApplication _fixture;

		public MemoryMvc([NotNull] RemoteServiceFixtures.BasicMvcApplication fixture, [NotNull] ITestOutputHelper testLogger)
		{
			_fixture = fixture;
			_fixture.TestLogger = testLogger;
			_fixture.Actions
			(
				setupConfiguration: () =>
				{
					var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
				},
				exerciseApplication: () =>
				{
					_fixture.Get();
					var startTime = DateTime.Now;
					while (DateTime.Now <= startTime.AddSeconds(60))
					{
						if (_fixture.AgentLog.GetMetrics().Any(metric => metric.MetricSpec.Name == "Memory/Physical"))
							break;
						Thread.Sleep(TimeSpan.FromSeconds(5));
					}
				}
			);
			_fixture.Initialize();
		}

		[Fact]
		public void Test()
		{
			var expectedMetrics = new List<Assertions.ExpectedMetric>
			{
				new Assertions.ExpectedMetric {metricName = @"Memory/Physical"}
			};

			var metrics = _fixture.AgentLog.GetMetrics().ToList();

			Assertions.MetricsExist(expectedMetrics, metrics);
		}
	}
}
