// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.CustomAttributes;

public class AspNetCoreCustomAttributesArraySupport : NewRelicIntegrationTest<RemoteServiceFixtures.AspNetCoreWebApiCustomAttributesFixture>
{
    private readonly RemoteServiceFixtures.AspNetCoreWebApiCustomAttributesFixture _fixture;

    public AspNetCoreCustomAttributesArraySupport(RemoteServiceFixtures.AspNetCoreWebApiCustomAttributesFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _fixture.Actions(
            setupConfiguration: () =>
            {
                var configPath = fixture.DestinationNewRelicConfigFilePath;
                var configModifier = new NewRelicConfigModifier(configPath);
                configModifier.ForceTransactionTraces();
                configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
                configModifier.ConfigureFasterErrorTracesHarvestCycle(10);
                configModifier.ConfigureFasterSpanEventsHarvestCycle(10);
                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
            },
            exerciseApplication: () =>
            {
                // Space each slow request across separate harvest cycles so each gets its own transaction trace.
                _fixture.GetCustomArrayAttributes();
                _fixture.AgentLog.WaitForLogLine(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(1));

                _fixture.GetCustomEmptyArrayAttributes();
                _fixture.AgentLog.WaitForLogLines(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(1), 2);

                _fixture.GetCustomArrayWithNulls();
                _fixture.AgentLog.WaitForLogLines(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(1), 3);

                // Wait for span event data to be harvested
                _fixture.AgentLog.WaitForLogLine(AgentLogFile.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(1));
            });
        _fixture.Initialize();
    }

    [Fact]
    public void Test_ArrayAttributes_AppearInTransactionTrace()
    {
        var expectedTransactionName = @"WebTransaction/MVC/AttributeTesting/CustomArrayAttributes";

        var transactionSample = _fixture.AgentLog.GetTransactionSamples()
            .Where(sample => sample.Path == expectedTransactionName)
            .FirstOrDefault();

        Assert.NotNull(transactionSample);

        NrAssert.Multiple(
            () => Assertions.TransactionTraceHasAttributes(new Dictionary<string, object>
            {
                { "stringArray", new[] { "red", "green", "blue" } }
            }, TransactionTraceAttributeType.User, transactionSample),
            () => Assertions.TransactionTraceHasAttributes(new Dictionary<string, object>
            {
                { "intArray", new[] { 1, 2, 3, 4, 5 } }
            }, TransactionTraceAttributeType.User, transactionSample),
            () => Assertions.TransactionTraceHasAttributes(new Dictionary<string, object>
            {
                { "boolArray", new[] { true, false, true } }
            }, TransactionTraceAttributeType.User, transactionSample)
        );
    }

    [Fact]
    public void Test_ArrayAttributes_AppearInTransactionEvent()
    {
        var expectedTransactionName = @"WebTransaction/MVC/AttributeTesting/CustomArrayAttributes";

        var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(expectedTransactionName);

        Assert.NotNull(transactionEvent);

        NrAssert.Multiple(
            () => Assertions.TransactionEventHasAttributes(new Dictionary<string, object>
            {
                { "stringArray", new[] { "red", "green", "blue" } }
            }, TransactionEventAttributeType.User, transactionEvent),
            () => Assertions.TransactionEventHasAttributes(new Dictionary<string, object>
            {
                { "intArray", new[] { 1, 2, 3, 4, 5 } }
            }, TransactionEventAttributeType.User, transactionEvent),
            () => Assertions.TransactionEventHasAttributes(new Dictionary<string, object>
            {
                { "boolArray", new[] { true, false, true } }
            }, TransactionEventAttributeType.User, transactionEvent)
        );
    }

    [Fact]
    public void Test_ArrayAttributes_AppearInSpanEvent()
    {
        var expectedTransactionName = @"WebTransaction/MVC/AttributeTesting/CustomArrayAttributes";

        var spanEvents = _fixture.AgentLog.GetSpanEvents().ToList();
        var rootSpan = spanEvents.FirstOrDefault(se =>
            se.IntrinsicAttributes.ContainsKey("nr.entryPoint") &&
            se.IntrinsicAttributes["name"]?.ToString() == expectedTransactionName);

        Assert.NotNull(rootSpan);

        NrAssert.Multiple(
            () => Assertions.SpanEventHasAttributes(new Dictionary<string, object>
            {
                { "stringArray", new[] { "red", "green", "blue" } }
            }, SpanEventAttributeType.User, rootSpan),
            () => Assertions.SpanEventHasAttributes(new Dictionary<string, object>
            {
                { "intArray", new[] { 1, 2, 3, 4, 5 } }
            }, SpanEventAttributeType.User, rootSpan),
            () => Assertions.SpanEventHasAttributes(new Dictionary<string, object>
            {
                { "boolArray", new[] { true, false, true } }
            }, SpanEventAttributeType.User, rootSpan)
        );
    }

    [Fact]
    public void Test_EmptyAndNullOnlyArrays_AreSkipped()
    {
        var expectedTransactionName = @"WebTransaction/MVC/AttributeTesting/CustomEmptyArrayAttributes";

        var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(expectedTransactionName);

        Assert.NotNull(transactionEvent);

        var transactionSample = _fixture.AgentLog.GetTransactionSamples()
            .Where(sample => sample.Path == expectedTransactionName)
            .FirstOrDefault();

        Assert.NotNull(transactionSample);

        // Verify empty/null arrays don't appear (consistent with our JsonSerializerHelpers behavior)
        NrAssert.Multiple(
            () => Assert.False(TransactionEventHasAttribute("emptyArray", transactionEvent), "Empty array should be skipped in transaction event"),
            () => Assert.False(TransactionEventHasAttribute("nullOnlyArray", transactionEvent), "Null-only array should be skipped in transaction event"),
            () => Assert.False(TransactionTraceHasAttribute("emptyArray", transactionSample), "Empty array should be skipped in transaction trace"),
            () => Assert.False(TransactionTraceHasAttribute("nullOnlyArray", transactionSample), "Null-only array should be skipped in transaction trace")
        );
    }

    [Fact]
    public void Test_ArraysWithNulls_FilterNullElements()
    {
        var expectedTransactionName = @"WebTransaction/MVC/AttributeTesting/CustomArrayWithNulls";

        var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(expectedTransactionName);

        Assert.NotNull(transactionEvent);

        NrAssert.Multiple(
            () => Assertions.TransactionEventHasAttributes(new Dictionary<string, object>
            {
                { "arrayWithNulls", new[] { "first", "third" } } // nulls filtered out
            }, TransactionEventAttributeType.User, transactionEvent),
            () => Assertions.TransactionEventHasAttributes(new Dictionary<string, object>
            {
                { "listAttribute", new[] { "list1", "list2", "list3" } } // List<T> works as array
            }, TransactionEventAttributeType.User, transactionEvent)
        );

        var transactionSample = _fixture.AgentLog.GetTransactionSamples()
            .Where(sample => sample.Path == expectedTransactionName)
            .FirstOrDefault();

        Assert.NotNull(transactionSample);

        NrAssert.Multiple(
            () => Assertions.TransactionTraceHasAttributes(new Dictionary<string, object>
            {
                { "arrayWithNulls", new[] { "first", "third" } }
            }, TransactionTraceAttributeType.User, transactionSample),
            () => Assertions.TransactionTraceHasAttributes(new Dictionary<string, object>
            {
                { "listAttribute", new[] { "list1", "list2", "list3" } }
            }, TransactionTraceAttributeType.User, transactionSample)
        );
    }

    // Helper methods to check if attributes exist
    private bool TransactionTraceHasAttribute(string attributeName, TransactionSample sample)
    {
        return sample.TraceData.Attributes.UserAttributes.ContainsKey(attributeName);
    }

    private bool TransactionEventHasAttribute(string attributeName, dynamic transactionEvent)
    {
        return transactionEvent.UserAttributes.ContainsKey(attributeName);
    }
}
