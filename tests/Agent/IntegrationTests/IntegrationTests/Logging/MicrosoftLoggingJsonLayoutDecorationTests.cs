// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging
{
    public abstract class MicrosoftLoggingJsonLayoutDecorationTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private readonly bool _decorationEnabled;

        public MicrosoftLoggingJsonLayoutDecorationTestsBase(TFixture fixture, ITestOutputHelper output, bool decorationEnabled) : base(fixture)
        {
            _decorationEnabled = decorationEnabled;
            _fixture = fixture;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework MICROSOFTLOGGING");
            _fixture.AddCommand($"LoggingTester ConfigureJsonLayoutAppenderForDecoration");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage DecorateMe DEBUG");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier
                    .EnableApplicationLogging()
                    .EnableLogDecoration(_decorationEnabled)
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
    public class MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCoreLatestTests : MicrosoftLoggingJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCore50Tests : MicrosoftLoggingJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCore31Tests : MicrosoftLoggingJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCore22Tests : MicrosoftLoggingJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public MicrosoftLoggingJsonLayoutDecorationEnabledTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    #endregion

    #region Disabled Tests

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationDisabledTestsNetCoreLatestTests : MicrosoftLoggingJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MicrosoftLoggingJsonLayoutDecorationDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationDisabledTestsNetCore50Tests : MicrosoftLoggingJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MicrosoftLoggingJsonLayoutDecorationDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationDisabledTestsNetCore31Tests : MicrosoftLoggingJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MicrosoftLoggingJsonLayoutDecorationDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftLoggingJsonLayoutDecorationDisabledTestsNetCore22Tests : MicrosoftLoggingJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public MicrosoftLoggingJsonLayoutDecorationDisabledTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    #endregion


}
