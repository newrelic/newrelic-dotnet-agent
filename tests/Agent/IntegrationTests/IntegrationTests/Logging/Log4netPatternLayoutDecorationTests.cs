// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text.RegularExpressions;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging
{
    public abstract class Log4netPatternLayoutDecorationTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public Log4netPatternLayoutDecorationTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"Log4netTester ConfigurePatternLayoutAppenderForDecoration");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage DecorateMe DEBUG");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier
                    .EnableApplicationLogging()
                    .EnableLogDecoration()
                    .EnableDistributedTrace()
                    .SetLogLevel("debug");
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void LogIsDecorated()
        {
            // Sample decorated data we are looking for:
            // "NR-LINKING-METADATA: {entity.guid=MjczMDcwfEFQTXxBUFBMSUNBVElPTnwxODQyMg, hostname=blah.hsd1.ca.comcast.net}"
            var regex = new Regex("NR-LINKING-METADATA: {entity\\.guid=.*hostname=.*}");
            Assert.Matches(regex, _fixture.RemoteApplication.CapturedOutput.StandardOutput);
        }
    }

    [NetFrameworkTest]
    public class Log4netPatternLayoutDecorationTestsFWLatestTests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4netPatternLayoutDecorationTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netPatternLayoutDecorationTestsFW471Tests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4netPatternLayoutDecorationTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netPatternLayoutDecorationTestsFW462Tests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public Log4netPatternLayoutDecorationTestsFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationTestsNetCoreLatestTests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4netPatternLayoutDecorationTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationTestsNetCore50Tests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4netPatternLayoutDecorationTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationTestsNetCore31Tests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4netPatternLayoutDecorationTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationTestsNetCore22Tests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public Log4netPatternLayoutDecorationTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationTestsNetCore21Tests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore21>
    {
        public Log4netPatternLayoutDecorationTestsNetCore21Tests(ConsoleDynamicMethodFixtureCore21 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
