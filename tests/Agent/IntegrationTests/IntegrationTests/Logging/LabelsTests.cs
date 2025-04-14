// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.Logging.Labels
{
    public abstract class LabelsTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private LoggingFramework _loggingFramework;
        private bool _labelsEnabled;
        private List<string> _excludes;

        private const string InfoMessage = "HelloWorld";

        private Dictionary<string, string> TestLabels => new Dictionary<string, string>()
        {
            { "mylabel1", "foo" },
            { "mylabel2", "bar" },
            { "mylabel3", "test" },
            { "mylabel4", "value" },
        };

        public LabelsTestsBase(TFixture fixture, ITestOutputHelper output, LoggingFramework loggingFramework, bool labelsEnabled, string excludes) : base(fixture)
        {
            _fixture = fixture;
            _loggingFramework = loggingFramework;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;
            _labelsEnabled = labelsEnabled;
            _excludes = excludes?.Split([','], StringSplitOptions.RemoveEmptyEntries).ToList();

            _fixture.AddCommand($"LoggingTester SetFramework {loggingFramework} {RandomPortGenerator.NextPort()}");
            _fixture.AddCommand($"LoggingTester Configure");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage {InfoMessage} INFO");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier
                    .EnableContextData(true)
                    .EnableApplicationLoggingForwardLabels(labelsEnabled)
                    .SetLabels(TestLabels)
                    .SetLogLevel("debug");

                    if (_excludes.Count > 0)
                    {
                        configModifier.SetApplicationLoggingForwardLabelsExcludes(excludes);
                    }
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.LogDataLogLineRegex, TimeSpan.FromSeconds(30));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var logData = _fixture.AgentLog.GetLogEventData().FirstOrDefault();

            Assert.NotNull(logData);
            Assert.NotNull(logData.Common);
            Assert.NotNull(logData.Common.Attributes);

            var attributes = logData.Common.Attributes;

            if (_labelsEnabled && _excludes.Count == 0)
            {
                Assert.Equal(7, attributes.Keys.Count); // 4 labels + 3 required attributes
                Assert.Equal(TestLabels["mylabel1"], attributes["tags.mylabel1"]);
                Assert.Equal(TestLabels["mylabel2"], attributes["tags.mylabel2"]);
                Assert.Equal(TestLabels["mylabel3"], attributes["tags.mylabel3"]);
                Assert.Equal(TestLabels["mylabel4"], attributes["tags.mylabel4"]);
            }
            else if (_labelsEnabled && _excludes.Count == 2)
            {
                Assert.Equal(5, attributes.Keys.Count); // 4 labels - 2 excludes + 3 required attributes
                Assert.False(attributes.ContainsKey("tags.mylabel1"));
                Assert.False(attributes.ContainsKey("tags.mylabel2"));
                Assert.Equal(TestLabels["mylabel3"], attributes["tags.mylabel3"]);
                Assert.Equal(TestLabels["mylabel4"], attributes["tags.mylabel4"]);
            }
            else
            {
                Assert.Equal(3, attributes.Keys.Count); // 3 required attributes
            }
        }
    }

    #region Serilog

    public class SerilogLabelsEnabledFWLatestTests : LabelsTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogLabelsEnabledFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog, true, string.Empty)
        {
        }
    }

    public class SerilogLabelsEnabledWithExcludesFWLatestTests : LabelsTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogLabelsEnabledWithExcludesFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog, true, "mylabel1,MYLABEL2")
        {
        }
    }

    public class SerilogLabelsDisabledFWLatestTests : LabelsTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public SerilogLabelsDisabledFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog, false, string.Empty)
        {
        }
    }

    public class SerilogLabelsEnabledNetCoreLatestTests : LabelsTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogLabelsEnabledNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog, true, string.Empty)
        {
        }
    }

    public class SerilogLabelsEnabledWithExcludesNetCoreLatestTests : LabelsTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogLabelsEnabledWithExcludesNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog, true, "mylabel1,MYLABEL2")
        {
        }
    }

    public class SerilogLabelsDisabledNetCoreLatestTests : LabelsTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public SerilogLabelsDisabledNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, LoggingFramework.Serilog, false, string.Empty)
        {
        }
    }

    #endregion

}
