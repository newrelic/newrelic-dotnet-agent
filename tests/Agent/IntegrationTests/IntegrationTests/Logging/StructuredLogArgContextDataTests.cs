// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.Logging.StructuredLogArgContextData;

/// <summary>
/// Verifies that structured log message arguments (e.g., logger.LogInformation("Person {Name} has id {Id}", name, id))
/// are extracted as context data attributes when using supported logging frameworks.
/// </summary>
public abstract class StructuredLogArgContextDataTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly TFixture _fixture;
    private readonly LoggingFramework _loggingFramework;

    private string _expectedName = "TestUser";
    private string _expectedId = "12345";
    // This unrealistic message format (no whitespace) is intentional since it is passed to the ConsoleMultiFunctionApplication as a
    // command line argument and we want to avoid any issues with argument parsing. The test is focused on verifying that the
    // structured arguments are extracted as context data, so the exact message template is not important as long as it is
    // consistent between the log message creation and the expected log line.
    private string _messageTemplate = "Person{Name}HasId={Id}";
    private string _expectedMessage  = "PersonTestUserHasId=12345";

    public StructuredLogArgContextDataTestsBase(TFixture fixture, ITestOutputHelper output, LoggingFramework loggingFramework) : base(fixture)
    {
        _fixture = fixture;
        _loggingFramework = loggingFramework;
        _fixture.SetTimeout(TimeSpan.FromMinutes(2));
        _fixture.TestLogger = output;

        // Some logging frameworks (Serilog and older versions of NLog) add quotes around structured arguments
        // This can be disabled by tweaking the template
        if (new[] { LoggingFramework.Serilog,
                     LoggingFramework.NLog,
                     LoggingFramework.SerilogEL,
                     LoggingFramework.NLogEL }.Contains(_loggingFramework))
        {
            _messageTemplate = "Person{Name:l}HasId={Id:l}";
        }

        _fixture.AddCommand($"LoggingTester SetFramework {loggingFramework} {RandomPortGenerator.NextPort()}");
        _fixture.AddCommand($"LoggingTester Configure");
        _fixture.AddCommand($"LoggingTester CreateSingleLogMessageWithStructuredArgs {_messageTemplate} {string.Join(",", _expectedName, _expectedId)}");
        _fixture.AddActions
        (
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                configModifier
                    .EnableContextData(true)
                    .SetLogLevel("debug");
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.LogDataLogLineRegex, TimeSpan.FromSeconds(30));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void StructuredLogArgsAppearAsContextData()
    {
        var expectedLogLines = new List<Assertions.ExpectedLogLine>
        {
            new Assertions.ExpectedLogLine
            {
                Level = LogUtils.GetLevelName(_loggingFramework, "INFO"),
                LogMessage = _expectedMessage,
                Attributes = new Dictionary<string, string>
                {
                    { "context.Name", _expectedName },
                    { "context.Id", _expectedId },
                }
            }
        };

        var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();

        // There should be only one log line
        Assert.True(logLines.Length == 1, $"Expected exactly one log line, but found {logLines.Length}.");

        Assertions.LogLinesExist(expectedLogLines, logLines, ignoreAttributeCount: true);
    }
}

#region MEL

public class MELStructuredLogArgContextDataNetCoreLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MELStructuredLogArgContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.MicrosoftLogging)
    {
    }
}

public class MELStructuredLogArgContextDataNetCoreOldestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public MELStructuredLogArgContextDataNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.MicrosoftLogging)
    {
    }
}

#endregion

#region Serilog

public class SerilogStructuredLogArgContextDataFWLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public SerilogStructuredLogArgContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.Serilog)
    {
    }
}

public class SerilogStructuredLogArgContextDataNetCoreLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public SerilogStructuredLogArgContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.Serilog)
    {
    }
}

public class SerilogStructuredLogArgContextDataNetCoreOldestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public SerilogStructuredLogArgContextDataNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.Serilog)
    {
    }
}

#endregion

#region NLog

public class NLogStructuredLogArgContextDataFWLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public NLogStructuredLogArgContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLog)
    {
    }
}

public class NLogStructuredLogArgContextDataNetCoreLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public NLogStructuredLogArgContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLog)
    {
    }
}

public class NLogStructuredLogArgContextDataNetCoreOldestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public NLogStructuredLogArgContextDataNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLog)
    {
    }
}

#endregion

#region MelWithSerilog

public class SerilogELStructuredLogArgContextDataFWLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public SerilogELStructuredLogArgContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.SerilogEL)
    {
    }
}

public class SerilogELStructuredLogArgContextDataFW48Tests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureFW48>
{
    public SerilogELStructuredLogArgContextDataFW48Tests(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.SerilogEL)
    {
    }
}

public class SerilogELStructuredLogArgContextDataNetCoreLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public SerilogELStructuredLogArgContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.SerilogEL)
    {
    }
}

public class SerilogELStructuredLogArgContextDataNetCoreOldestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public SerilogELStructuredLogArgContextDataNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.SerilogEL)
    {
    }
}

#endregion

#region MelWithNLog

public class NLogELStructuredLogArgContextDataFWLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public NLogELStructuredLogArgContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLogEL)
    {
    }
}

public class NLogELStructuredLogArgContextDataNetCoreLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public NLogELStructuredLogArgContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLogEL)
    {
    }
}

#endregion
