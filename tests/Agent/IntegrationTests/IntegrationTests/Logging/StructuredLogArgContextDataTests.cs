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
    private readonly bool _includeContext;

    private string _expectedName = "TestUser";
    private string _expectedId = "12345";
    // This unrealistic message format (no whitespace) is intentional since it is passed to the ConsoleMultiFunctionApplication as a
    // command line argument and we want to avoid any issues with argument parsing. The test is focused on verifying that the
    // structured arguments are extracted as context data, so the exact message template is not important as long as it is
    // consistent between the log message creation and the expected log line.
    private string _messageTemplate = "Person{Name}HasId={Id}";
    private string _expectedMessage  = "PersonTestUserHasId=12345";

    private Dictionary<string, string> GetExpectedAttributes(LoggingFramework framework) => new Dictionary<string, string>()
    {
        { "framework", framework.ToString() },
        { "mycontext1", "foo" },
        { "mycontext2", "bar" },
        { "mycontext3", "test" },
        { "mycontext4", "value" },
    };

    private string FlattenExpectedAttributes(Dictionary<string, string> attributes) => string.Join(",", attributes.Select(x => x.Key + "=" + x.Value).ToArray());


    public StructuredLogArgContextDataTestsBase(TFixture fixture, ITestOutputHelper output, LoggingFramework loggingFramework, bool includeContext) : base(fixture)
    {
        _fixture = fixture;
        _loggingFramework = loggingFramework;
        _includeContext = includeContext;
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
        if (_includeContext)
        {
            var expectedAttributes = GetExpectedAttributes(loggingFramework);
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageWithStructuredArgsAndContext {_messageTemplate} {string.Join(",", _expectedName, _expectedId)} {FlattenExpectedAttributes(expectedAttributes)}");
        }
        else
        {
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessageWithStructuredArgs {_messageTemplate} {string.Join(",", _expectedName, _expectedId)}");
        }
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
        var expectedAttributes = new Dictionary<string, string>
                {
                    { "context.Name", _expectedName },
                    { "context.Id", _expectedId },
                };

        if (_includeContext) {
            foreach ( var contextAttribute in GetExpectedAttributes(_loggingFramework) ) {
                expectedAttributes.Add(contextAttribute.Key, contextAttribute.Value);
            }
        }

        var expectedLogLines = new List<Assertions.ExpectedLogLine>
        {
            new Assertions.ExpectedLogLine
            {
                Level = LogUtils.GetLevelName(_loggingFramework, "INFO"),
                LogMessage = _expectedMessage,
                Attributes = expectedAttributes
            }
        };

        var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();

        // There should be only one log line
        Assert.True(logLines.Length == 1, $"Expected exactly one log line, but found {logLines.Length}.");

        // Serilog.Extensions.Logging ends up with an additional context attribute called ContextDataSource
        // so we ignore the attribute count in that case
        var ignoreAttributeCount = _loggingFramework == LoggingFramework.SerilogEL ? true : false;

        Assertions.LogLinesExist(expectedLogLines, logLines, ignoreAttributeCount: ignoreAttributeCount);
    }
}

#region MEL

public class MELStructuredLogArgContextDataWithContextNetCoreLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MELStructuredLogArgContextDataWithContextNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.MicrosoftLogging, true)
    {
    }
}

public class MELStructuredLogArgContextDataNoContextNetCoreLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MELStructuredLogArgContextDataNoContextNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.MicrosoftLogging, false)
    {
    }
}

public class MELStructuredLogArgContextDataWithContextNetCoreOldestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public MELStructuredLogArgContextDataWithContextNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.MicrosoftLogging, true)
    {
    }
}

public class MELStructuredLogArgContextDataNoContextNetCoreOldestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public MELStructuredLogArgContextDataNoContextNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.MicrosoftLogging, false)
    {
    }
}

#endregion

#region Serilog

public class SerilogStructuredLogArgContextDataWithContextFWLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public SerilogStructuredLogArgContextDataWithContextFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.Serilog, true)
    {
    }
}

public class SerilogStructuredLogArgContextDataNoContextFWLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public SerilogStructuredLogArgContextDataNoContextFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.Serilog, false)
    {
    }
}

public class SerilogStructuredLogArgContextDataWithContextNetCoreLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public SerilogStructuredLogArgContextDataWithContextNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.Serilog, true)
    {
    }
}

public class SerilogStructuredLogArgContextDataNoContextNetCoreLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public SerilogStructuredLogArgContextDataNoContextNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.Serilog, false)
    {
    }
}

public class SerilogStructuredLogArgContextDataWithContextNetCoreOldestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public SerilogStructuredLogArgContextDataWithContextNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.Serilog, true)
    {
    }
}

public class SerilogStructuredLogArgContextDataNoContextNetCoreOldestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public SerilogStructuredLogArgContextDataNoContextNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.Serilog, false)
    {
    }
}

#endregion

#region NLog

public class NLogStructuredLogArgContextDataWithContextFWLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public NLogStructuredLogArgContextDataWithContextFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLog, true)
    {
    }
}

public class NLogStructuredLogArgContextDataNoContextFWLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public NLogStructuredLogArgContextDataNoContextFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLog, false)
    {
    }
}

public class NLogStructuredLogArgContextDataWithContextNetCoreLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public NLogStructuredLogArgContextDataWithContextNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLog, true)
    {
    }
}

public class NLogStructuredLogArgContextDataNoContextNetCoreLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public NLogStructuredLogArgContextDataNoContextNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLog, false)
    {
    }
}

public class NLogStructuredLogArgContextDataWithContextNetCoreOldestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public NLogStructuredLogArgContextDataWithContextNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLog, true)
    {
    }
}

public class NLogStructuredLogArgContextDataNoContextNetCoreOldestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public NLogStructuredLogArgContextDataNoContextNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLog, false)
    {
    }
}

#endregion

#region MelWithSerilog

public class SerilogELStructuredLogArgContextDataWithContextFWLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public SerilogELStructuredLogArgContextDataWithContextFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.SerilogEL, true)
    {
    }
}

public class SerilogELStructuredLogArgContextDataNoContextFWLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public SerilogELStructuredLogArgContextDataNoContextFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.SerilogEL, false)
    {
    }
}

public class SerilogELStructuredLogArgContextDataWithContextFW48Tests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureFW48>
{
    public SerilogELStructuredLogArgContextDataWithContextFW48Tests(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.SerilogEL, true)
    {
    }
}

public class SerilogELStructuredLogArgContextDataNoContextFW48Tests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureFW48>
{
    public SerilogELStructuredLogArgContextDataNoContextFW48Tests(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.SerilogEL, false)
    {
    }
}

public class SerilogELStructuredLogArgContextDataWithContextNetCoreLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public SerilogELStructuredLogArgContextDataWithContextNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.SerilogEL, true)
    {
    }
}

public class SerilogELStructuredLogArgContextDataNoContextNetCoreLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public SerilogELStructuredLogArgContextDataNoContextNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.SerilogEL, false)
    {
    }
}

public class SerilogELStructuredLogArgContextDataWithContextNetCoreOldestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public SerilogELStructuredLogArgContextDataWithContextNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.SerilogEL, true)
    {
    }
}

public class SerilogELStructuredLogArgContextDataNoContextNetCoreOldestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public SerilogELStructuredLogArgContextDataNoContextNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.SerilogEL, false)
    {
    }
}

#endregion

#region MelWithNLog

public class NLogELStructuredLogArgContextDataWithContextFWLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public NLogELStructuredLogArgContextDataWithContextFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLogEL, true)
    {
    }
}

public class NLogELStructuredLogArgContextDataNoContextFWLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public NLogELStructuredLogArgContextDataNoContextFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLogEL, false)
    {
    }
}

public class NLogELStructuredLogArgContextDataWithContextNetCoreLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public NLogELStructuredLogArgContextDataWithContextNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLogEL, true)
    {
    }
}

public class NLogELStructuredLogArgContextDataNoContextNetCoreLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public NLogELStructuredLogArgContextDataNoContextNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.NLogEL, false)
    {
    }
}

#endregion
