// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging
{
    public abstract class SerilogJsonLayoutDecorationTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private readonly bool _decorationEnabled;

        public SerilogJsonLayoutDecorationTestsBase(TFixture fixture, ITestOutputHelper output, bool decorationEnabled) : base(fixture)
        {
            _decorationEnabled = decorationEnabled;
            _fixture = fixture;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework Serilog");
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
    [NetFrameworkTest]
    public class SerilogJsonLayoutDecorationEnabledTestsFWLatestTests : SerilogJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogJsonLayoutDecorationEnabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogJsonLayoutDecorationEnabledTestsFW471Tests : SerilogJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogJsonLayoutDecorationEnabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationEnabledTestsNetCoreLatestTests : SerilogJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogJsonLayoutDecorationEnabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationEnabledTestsNetCore50Tests : SerilogJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogJsonLayoutDecorationEnabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationEnabledTestsNetCore31Tests : SerilogJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogJsonLayoutDecorationEnabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationEnabledTestsNetCore22Tests : SerilogJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public SerilogJsonLayoutDecorationEnabledTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }
    #endregion

    #region Disabled Tests
    [NetFrameworkTest]
    public class SerilogJsonLayoutDecorationDisabledTestsFWLatestTests : SerilogJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogJsonLayoutDecorationDisabledTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogJsonLayoutDecorationDisabledTestsFW471Tests : SerilogJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public SerilogJsonLayoutDecorationDisabledTestsFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationDisabledTestsNetCoreLatestTests : SerilogJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogJsonLayoutDecorationDisabledTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationDisabledTestsNetCore50Tests : SerilogJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public SerilogJsonLayoutDecorationDisabledTestsNetCore50Tests(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationDisabledTestsNetCore31Tests : SerilogJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public SerilogJsonLayoutDecorationDisabledTestsNetCore31Tests(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }

    [NetCoreTest]
    public class SerilogJsonLayoutDecorationDisabledTestsNetCore22Tests : SerilogJsonLayoutDecorationTestsBase<ConsoleDynamicMethodFixtureCore22>
    {
        public SerilogJsonLayoutDecorationDisabledTestsNetCore22Tests(ConsoleDynamicMethodFixtureCore22 fixture, ITestOutputHelper output)
            : base(fixture, output, true)
        {
        }
    }
    #endregion


}
