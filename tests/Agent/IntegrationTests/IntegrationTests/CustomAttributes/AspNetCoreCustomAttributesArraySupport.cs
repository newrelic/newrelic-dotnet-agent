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
                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
            },
            exerciseApplication: () =>
            {
                _fixture.GetCustomArrayAttributes();
                _fixture.GetCustomEmptyArrayAttributes();
                _fixture.GetCustomArrayWithNulls();
                _fixture.GetCustomArrayErrorAttributes();

                _fixture.AgentLog.WaitForLogLine(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(1));
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
    public void Test_EmptyAndNullOnlyArrays_AreSkipped()
    {
        var expectedTransactionName = @"WebTransaction/MVC/AttributeTesting/CustomEmptyArrayAttributes";

        var transactionSample = _fixture.AgentLog.GetTransactionSamples()
            .Where(sample => sample.Path == expectedTransactionName)
            .FirstOrDefault();

        var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(expectedTransactionName);

        Assert.NotNull(transactionSample);
        Assert.NotNull(transactionEvent);

        // Verify empty/null arrays don't appear (consistent with our JsonSerializerHelpers behavior)
        NrAssert.Multiple(
            () => Assert.False(TransactionTraceHasAttribute("emptyArray", transactionSample), "Empty array should be skipped in transaction trace"),
            () => Assert.False(TransactionTraceHasAttribute("nullOnlyArray", transactionSample), "Null-only array should be skipped in transaction trace"),
            () => Assert.False(TransactionEventHasAttribute("emptyArray", transactionEvent), "Empty array should be skipped in transaction event"),
            () => Assert.False(TransactionEventHasAttribute("nullOnlyArray", transactionEvent), "Null-only array should be skipped in transaction event")
        );
    }

    [Fact]
    public void Test_ArraysWithNulls_FilterNullElements()
    {
        var expectedTransactionName = @"WebTransaction/MVC/AttributeTesting/CustomArrayWithNulls";

        var transactionSample = _fixture.AgentLog.GetTransactionSamples()
            .Where(sample => sample.Path == expectedTransactionName)
            .FirstOrDefault();

        var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(expectedTransactionName);

        Assert.NotNull(transactionSample);
        Assert.NotNull(transactionEvent);

        NrAssert.Multiple(
            () => Assertions.TransactionTraceHasAttributes(new Dictionary<string, object>
            {
                { "arrayWithNulls", new[] { "first", "third" } } // nulls filtered out
            }, TransactionTraceAttributeType.User, transactionSample),
            () => Assertions.TransactionEventHasAttributes(new Dictionary<string, object>
            {
                { "arrayWithNulls", new[] { "first", "third" } } // nulls filtered out
            }, TransactionEventAttributeType.User, transactionEvent),
            () => Assertions.TransactionTraceHasAttributes(new Dictionary<string, object>
            {
                { "listAttribute", new[] { "list1", "list2", "list3" } } // List<T> works as array
            }, TransactionTraceAttributeType.User, transactionSample),
            () => Assertions.TransactionEventHasAttributes(new Dictionary<string, object>
            {
                { "listAttribute", new[] { "list1", "list2", "list3" } } // List<T> works as array
            }, TransactionEventAttributeType.User, transactionEvent)
        );
    }

    [Fact]
    public void Test_ArrayAttributes_AppearInErrorTraceAndEvents()
    {
        var expectedErrorPath = @"WebTransaction/MVC/AttributeTesting/CustomArrayErrorAttributes";

        var errorTrace = _fixture.AgentLog.GetErrorTraces()
            .Where(trace => trace.Path == expectedErrorPath)
            .FirstOrDefault();

        var errorEvents = _fixture.AgentLog.GetErrorEvents().ToList();
        var arrayErrorEvent = errorEvents
            .Where(evt => evt.IntrinsicAttributes.ContainsKey("transactionName") &&
                         evt.IntrinsicAttributes["transactionName"].ToString() == expectedErrorPath)
            .FirstOrDefault();

        Assert.NotNull(errorTrace);
        Assert.NotNull(arrayErrorEvent);

        // Note: Error assertions don't currently support object values, so we verify arrays exist manually
        Assert.True(errorTrace.Attributes.UserAttributes.ContainsKey("errorTags"));
        Assert.True(errorTrace.Attributes.UserAttributes.ContainsKey("errorCodes"));
        Assert.True(arrayErrorEvent.UserAttributes.ContainsKey("errorTags"));
        Assert.True(arrayErrorEvent.UserAttributes.ContainsKey("errorCodes"));

        // Verify the arrays have the expected content
        var errorTagsTrace = errorTrace.Attributes.UserAttributes["errorTags"] as object[];
        var errorCodesTrace = errorTrace.Attributes.UserAttributes["errorCodes"] as object[];
        var errorTagsEvent = arrayErrorEvent.UserAttributes["errorTags"] as object[];
        var errorCodesEvent = arrayErrorEvent.UserAttributes["errorCodes"] as object[];

        Assert.Equal(new[] { "error", "critical", "timeout" }, errorTagsTrace);
        Assert.Equal(new object[] { 500, 503, 404 }, errorCodesTrace);
        Assert.Equal(new[] { "error", "critical", "timeout" }, errorTagsEvent);
        Assert.Equal(new object[] { 500, 503, 404 }, errorCodesEvent);
    }

    // Helper methods to check if attributes exist
    private bool TransactionTraceHasAttribute(string attributeName, dynamic transactionTrace)
    {
        return transactionTrace.TransactionTraceData.Attributes.UserAttributes.ContainsKey(attributeName);
    }

    private bool TransactionEventHasAttribute(string attributeName, dynamic transactionEvent)
    {
        return transactionEvent.UserAttributes.ContainsKey(attributeName);
    }
}