// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.BasicInstrumentation;

public abstract class AsyncStreamTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : RemoteServiceFixtures.AspNetCoreFeaturesFixture
{
    private readonly TFixture _fixture;

    protected AsyncStreamTestsBase(TFixture fixture, ITestOutputHelper output)
        : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _fixture.Actions
        (
            setupConfiguration: () =>
            {
                var instrumentationFilePath = Path.Combine(fixture.DestinationNewRelicExtensionsDirectoryPath, "CustomInstrumentation.xml");

                CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "AspNetCoreFeatures", "AspNetCoreFeatures.Controllers.AsyncStreamController", "GetNumbers");
                CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "AspNetCoreFeatures", "AspNetCoreFeatures.Controllers.AsyncStreamController", "DoSomethingAsync");
            },
            exerciseApplication: () =>
            {
                _fixture.AsyncStream();
            }
        );
        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        Assert.NotNull(metrics);
        Assertions.MetricsExist(_expectedMetrics, metrics);
    }

    private readonly List<Assertions.ExpectedMetric> _expectedMetrics = new List<Assertions.ExpectedMetric>
    {
        new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncStreamController/Get", CallCountAllHarvests = 1 },
        new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.AsyncStreamController/GetNumbers", CallCountAllHarvests = 1 },
        new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.AsyncStreamController/DoSomethingAsync", CallCountAllHarvests = 10 },
        new Assertions.ExpectedMetric { metricName = @"DotNet/AsyncStreamController/Get", metricScope = "WebTransaction/MVC/AsyncStream/Get", CallCountAllHarvests = 1 },
        new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.AsyncStreamController/GetNumbers", metricScope = "WebTransaction/MVC/AsyncStream/Get", CallCountAllHarvests = 1 },
        new Assertions.ExpectedMetric { metricName = @"DotNet/AspNetCoreFeatures.Controllers.AsyncStreamController/DoSomethingAsync", metricScope = "WebTransaction/MVC/AsyncStream/Get", CallCountAllHarvests = 10 }
    };
}

public class AsyncStreamTestsCoreOldest : AsyncStreamTestsBase<RemoteServiceFixtures.AspNetCoreFeaturesFixtureCoreOldest>
{
    public AsyncStreamTestsCoreOldest(RemoteServiceFixtures.AspNetCoreFeaturesFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class AsyncStreamTestsCoreLatest : AsyncStreamTestsBase<RemoteServiceFixtures.AspNetCoreFeaturesFixtureCoreLatest>
{
    public AsyncStreamTestsCoreLatest(RemoteServiceFixtures.AspNetCoreFeaturesFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
