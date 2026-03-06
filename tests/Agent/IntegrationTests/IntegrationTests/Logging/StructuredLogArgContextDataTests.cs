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
/// are extracted as context data attributes when using Microsoft.Extensions.Logging.
/// </summary>
public abstract class StructuredLogArgContextDataTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly TFixture _fixture;

    private const string ExpectedName = "TestUser";
    private const string ExpectedId = "12345";
    private const string ExpectedMessage = "Person TestUser has id 12345";

    public StructuredLogArgContextDataTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.SetTimeout(TimeSpan.FromMinutes(2));
        _fixture.TestLogger = output;

        _fixture.AddCommand($"LoggingTester SetFramework MicrosoftLogging {RandomPortGenerator.NextPort()}");
        _fixture.AddCommand($"LoggingTester Configure");
        _fixture.AddCommand($"LoggingTester CreateSingleLogMessageWithStructuredArgs {ExpectedName} {ExpectedId}");

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
                Level = "INFO",
                LogMessage = ExpectedMessage,
                Attributes = new Dictionary<string, string>
                {
                    { "Name", ExpectedName },
                    { "Id", ExpectedId },
                }
            }
        };

        var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();

        Assertions.LogLinesExist(expectedLogLines, logLines, ignoreAttributeCount: true);
    }
}

#region MEL

public class MELStructuredLogArgContextDataNetCoreLatestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MELStructuredLogArgContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class MELStructuredLogArgContextDataNetCoreOldestTests : StructuredLogArgContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public MELStructuredLogArgContextDataNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

#endregion
