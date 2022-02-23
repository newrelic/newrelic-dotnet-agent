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
        private readonly bool _decorationEnabled;

        public Log4netPatternLayoutDecorationTestsBase(TFixture fixture, ITestOutputHelper output, bool decorationEnabled) : base(fixture)
        {
            _fixture = fixture;
            _decorationEnabled = decorationEnabled;
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
                    .EnableLogDecoration(decorationEnabled)
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

            if (_decorationEnabled)
            {
                Assert.Matches(regex, _fixture.RemoteApplication.CapturedOutput.StandardOutput);
            }
            else
            {
                Assert.DoesNotMatch(regex, _fixture.RemoteApplication.CapturedOutput.StandardOutput);
            }
        }
    }

    #region Enabled Tests

    [NetFrameworkTest]
    public class Log4netPatternLayoutDecorationEnabledTestsFWLatestTests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4netPatternLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netPatternLayoutDecorationEnabledTestsFW471Tests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4netPatternLayoutDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationEnabledTestsNetCoreLatestTests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4netPatternLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationEnabledTestsNetCore50Tests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4netPatternLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationEnabledTestsNetCore31Tests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4netPatternLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationEnabledTestsNetCore22Tests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public Log4netPatternLayoutDecorationEnabledTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    #endregion

    #region Disabled Tests

    [NetFrameworkTest]
    public class Log4netPatternLayoutDecorationDisabledTestsFWLatestTests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public Log4netPatternLayoutDecorationDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netPatternLayoutDecorationDisabledTestsFW471Tests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public Log4netPatternLayoutDecorationDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationDisabledTestsNetCoreLatestTests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public Log4netPatternLayoutDecorationDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationDisabledTestsNetCore50Tests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public Log4netPatternLayoutDecorationDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationDisabledTestsNetCore31Tests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public Log4netPatternLayoutDecorationDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    [NetCoreTest]
    public class Log4netPatternLayoutDecorationDisabledTestsNetCore22Tests : Log4netPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public Log4netPatternLayoutDecorationDisabledTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    #endregion
}
