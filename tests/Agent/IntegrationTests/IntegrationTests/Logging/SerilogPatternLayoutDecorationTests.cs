// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging
{
    public abstract class SerilogPatternLayoutDecorationTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private readonly bool _decorationEnabled;

        public SerilogPatternLayoutDecorationTestsBase(TFixture fixture, ITestOutputHelper output, bool decorationEnabled) : base(fixture)
        {
            _fixture = fixture;
            _decorationEnabled = decorationEnabled;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework Serilog");
            _fixture.AddCommand($"LoggingTester ConfigurePatternLayoutAppenderForDecoration");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage DecorateMe DEBUG");

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
            // "NR-LINKING|MjczMDcwfEFQTXxBUFBMSUNBVElPTnwxODQyMg|blah.hsd1.ca.comcast.net|45f120972d61834b96fb890d2a8f97e7|840d9a82e8bc18a8|"
            var regex = new Regex(@"NR-LINKING\|[a-zA-Z0-9]*\|[a-zA-Z0-9._-]*\|[a-zA-Z0-9]*\|[a-zA-Z0-9]*\|");
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
    public class SerilogPatternLayoutDecorationEnabledTestsFWLatestTests : SerilogPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogPatternLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogPatternLayoutDecorationEnabledTestsFW471Tests : SerilogPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogPatternLayoutDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationEnabledTestsNetCoreLatestTests : SerilogPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogPatternLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationEnabledTestsNetCore50Tests : SerilogPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogPatternLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationEnabledTestsNetCore31Tests : SerilogPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogPatternLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationEnabledTestsNetCore22Tests : SerilogPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public SerilogPatternLayoutDecorationEnabledTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    #endregion

    #region Disabled Tests

    [NetFrameworkTest]
    public class SerilogPatternLayoutDecorationDisabledTestsFWLatestTests : SerilogPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogPatternLayoutDecorationDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogPatternLayoutDecorationDisabledTestsFW471Tests : SerilogPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogPatternLayoutDecorationDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationDisabledTestsNetCoreLatestTests : SerilogPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogPatternLayoutDecorationDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationDisabledTestsNetCore50Tests : SerilogPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogPatternLayoutDecorationDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationDisabledTestsNetCore31Tests : SerilogPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogPatternLayoutDecorationDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    [NetCoreTest]
    public class SerilogPatternLayoutDecorationDisabledTestsNetCore22Tests : SerilogPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public SerilogPatternLayoutDecorationDisabledTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    #endregion
}
