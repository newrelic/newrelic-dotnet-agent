// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.Logging.ContextData;

// Regression coverage for GitHub issue #3641: a context-data value that Newtonsoft.Json
// cannot serialize (e.g. a self-referencing object such as an ASP.NET Core Endpoint placed
// into a logging scope) must not corrupt the log_event_data payload. Before the fix, the
// agent wrote such a value's raw ToString() unquoted, producing invalid JSON and causing the
// collector to reject the entire batch. These tests assert the payload is well-formed JSON
// and that the offending value is emitted as a quoted string.
public abstract class ContextDataNonSerializableTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly TFixture _fixture;
    private readonly LoggingFramework _loggingFramework;

    private const string InfoMessage = "NonSerializableContextMessage";

    // Must match NonSerializableContextValue.ToStringValue in the shared application
    // (the test project does not reference the shared app, so the value is duplicated here).
    private const string NonSerializableToStringValue = "Packing.Api.Controllers.FeatureController.IsEnabled (Packing.Api)";

    protected ContextDataNonSerializableTestsBase(TFixture fixture, ITestOutputHelper output, LoggingFramework loggingFramework) : base(fixture)
    {
        _fixture = fixture;
        _loggingFramework = loggingFramework;
        _fixture.SetTimeout(TimeSpan.FromMinutes(2));
        _fixture.TestLogger = output;

        _fixture.AddCommand($"LoggingTester SetFramework {_loggingFramework} {RandomPortGenerator.NextPort()}");
        _fixture.AddCommand("LoggingTester Configure");
        _fixture.AddCommand($"LoggingTester CreateSingleLogMessageWithNonSerializableContext {_loggingFramework} {InfoMessage}");

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
    public void LogEventDataPayloadIsValidJson()
    {
        var payloads = _fixture.AgentLog.GetLogEventDataPayloads().ToArray();

        Assert.NotEmpty(payloads);

        // The core assertion: every log_event_data payload must parse as valid JSON, even
        // though one of the captured context-data values could not be serialized.
        foreach (var payload in payloads)
        {
            var exception = Record.Exception(() => JToken.Parse(payload));
            Assert.True(exception == null, $"log_event_data payload is not valid JSON: {exception?.Message}\nPayload: {payload}");
        }

        // The unserializable value must be present, captured as a quoted string equal to its
        // ToString(), and the normal sibling attribute must be unaffected.
        var expectedAttributes = new Dictionary<string, string>
        {
            { "normalkey", "normalvalue" },
            { "nonserializable", NonSerializableToStringValue },
        };

        var expectedLogLines = new[]
        {
            new Assertions.ExpectedLogLine
            {
                Level = LogUtils.GetLevelName(_loggingFramework, "INFO"),
                LogMessage = InfoMessage,
                Attributes = expectedAttributes
            }
        };

        var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();
        Assertions.LogLinesExist(expectedLogLines, logLines, ignoreAttributeCount: true);
    }
}

public class MELContextDataNonSerializableNetCoreLatestTests : ContextDataNonSerializableTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MELContextDataNonSerializableNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.MicrosoftLogging)
    {
    }
}

public class MELContextDataNonSerializableNetCoreOldestTests : ContextDataNonSerializableTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public MELContextDataNonSerializableNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.MicrosoftLogging)
    {
    }
}

public class MELContextDataNonSerializableFWLatestTests : ContextDataNonSerializableTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public MELContextDataNonSerializableFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, LoggingFramework.MicrosoftLogging)
    {
    }
}
