// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging
{
    public abstract class MicrosoftLoggingPatternLayoutDecorationTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private readonly bool _decorationEnabled;

        public MicrosoftLoggingPatternLayoutDecorationTestsBase(TFixture fixture, ITestOutputHelper output, bool decorationEnabled) : base(fixture)
        {
            _fixture = fixture;
            _decorationEnabled = decorationEnabled;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework MICROSOFTLOGGING");
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

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCoreLatestTests : MicrosoftLoggingPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCore50Tests : MicrosoftLoggingPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCore31Tests : MicrosoftLoggingPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCore22Tests : MicrosoftLoggingPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public MicrosoftLoggingPatternLayoutDecorationEnabledTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    #endregion

    #region Disabled Tests

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationDisabledTestsNetCoreLatestTests : MicrosoftLoggingPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MicrosoftLoggingPatternLayoutDecorationDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationDisabledTestsNetCore50Tests : MicrosoftLoggingPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MicrosoftLoggingPatternLayoutDecorationDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationDisabledTestsNetCore31Tests : MicrosoftLoggingPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MicrosoftLoggingPatternLayoutDecorationDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingPatternLayoutDecorationDisabledTestsNetCore22Tests : MicrosoftLoggingPatternLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public MicrosoftLoggingPatternLayoutDecorationDisabledTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output, false)
        {
        }
    }

    #endregion
}
