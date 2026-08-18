// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Labels;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.TestUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilities;

[TestFixture]
public class LogEventWireModelCollectionJsonConverterTests
{
    [Test]
    public void LogEventWireModelCollectionIsJsonSerializable()
    {
        var expected = """
                       {
                           "common": {
                               "attributes": {
                                   "entity.name": "myApplicationName",
                                   "entity.guid": "guid",
                                   "hostname": "hostname",
                                   "tags.label1": "value1",
                                   "tags.label2": "value2"
                               }
                           },
                           "logs": [{
                                   "timestamp": 1,
                                   "message": "TestMessage1",
                                   "level": "TestLevel",
                                   "span.id": "TestSpanId1",
                                   "trace.id": "TestTraceId1",
                                   "attributes": {
                                       "context.key1": "value1",
                                       "context.key2": 1,
                                       "context.key3": {
                                           "Foo": 1,
                                           "Bar": 2
                                       }
                                   }
                               }, {
                                   "timestamp": 1,
                                   "message": "TestMessage2",
                                   "level": "TestLevel",
                                   "span.id": "TestSpanId2",
                                   "trace.id": "TestTraceId2"
                               }, {
                                   "timestamp": 1,
                                   "message": "TestMessage3",
                                   "level": "TestLevel",
                                   "span.id": "TestSpanId3",
                                   "trace.id": "TestTraceId3"
                               }, {
                                   "timestamp": 1,
                                   "message": "TestMessage4",
                                   "level": "TestLevel",
                                   "error.stack": "foo \nbar",
                                   "error.message": "errorMessage",
                                   "error.class": "errorClass",
                                   "span.id": "TestSpanId4",
                                   "trace.id": "TestTracedId4",
                                   "attributes": {
                                       "context.key1": "value1",
                                       "context.key2": 1,
                                       "context.key3": {
                                           "Foo": 1,
                                           "Bar": 2
                                       }
                                   }
                               }
                           ]
                       }
                       """;

        var _contextData = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 }, {"key3", new { Foo = 1, Bar = 2 } } };
        var labels = new List<Label> { new Label("label1", "value1"), new Label("label2", "value2") };
        var sourceObject = new LogEventWireModelCollection(
            "myApplicationName",
            "guid",
            "hostname",
            labels,
            new List<LogEventWireModel>()
            {
                new LogEventWireModel(1, "TestMessage1", "TestLevel", "TestSpanId1", "TestTraceId1", _contextData),
                new LogEventWireModel(1, "TestMessage2", "TestLevel", "TestSpanId2", "TestTraceId2", null),
                new LogEventWireModel(1, "TestMessage3", "TestLevel", "TestSpanId3", "TestTraceId3", new Dictionary<string, object>()),
                new LogEventWireModel(1, "TestMessage4", "TestLevel", new string[] {"foo", "bar" }, "errorMessage", "errorClass", "TestSpanId4", "TestTracedId4", _contextData)
            });

        var serialized = JsonConvert.SerializeObject(sourceObject, Formatting.None);
        Assert.That(serialized, Is.EqualTo(expected.Condense()));
    }

    [Test]
    public void DeserializeObjectFailsWithNotImplementedException()
    {
        Assert.Throws<NotImplementedException>(() =>JsonConvert.DeserializeObject<LogEventWireModelCollection>("{}"));
    }

    // A context-data value whose object graph cannot be serialized by Newtonsoft (the self
    // reference triggers "Self referencing loop detected" under default settings, just like
    // the property graph of an ASP.NET Core Endpoint). Its ToString() contains characters
    // (spaces, parentheses) that would make an unquoted token unambiguously invalid JSON.
    private class NonSerializableContextValue
    {
        public const string ToStringValue = "Some.Namespace.Endpoint.Action (Assembly)";

        public NonSerializableContextValue Self { get; }

        public NonSerializableContextValue()
        {
            Self = this;
        }

        public override string ToString() => ToStringValue;
    }

    // A context-data value that cannot be serialized AND whose ToString() throws, forcing the
    // converter all the way to its type-name fallback.
    private class ToStringThrowsContextValue
    {
        public ToStringThrowsContextValue Self { get; }

        public ToStringThrowsContextValue()
        {
            Self = this;
        }

        public override string ToString() => throw new InvalidOperationException("ToString failed");
    }

    [Test]
    public void NonSerializableContextValue_IsWrittenAsQuotedString_AndPayloadIsValidJson()
    {
        var contextData = new Dictionary<string, object> { { "endpoint", new NonSerializableContextValue() } };
        var sourceObject = new LogEventWireModelCollection(
            "myApplicationName",
            "guid",
            "hostname",
            new List<Label>(),
            new List<LogEventWireModel>
            {
                new LogEventWireModel(1, "TestMessage", "TestLevel", "TestSpanId", "TestTraceId", contextData)
            });

        var serialized = JsonConvert.SerializeObject(sourceObject, Formatting.None);

        // The whole payload must remain valid JSON - a single unserializable value must not
        // corrupt the batch (which would cause the collector to reject every log in it).
        Assert.DoesNotThrow(() => JToken.Parse(serialized), "log_event_data payload must be valid JSON");

        var attribute = JObject.Parse(serialized)["logs"][0]["attributes"]["context.endpoint"];
        Assert.Multiple(() =>
        {
            Assert.That(attribute.Type, Is.EqualTo(JTokenType.String), "value must be a quoted JSON string");
            Assert.That(attribute.Value<string>(), Is.EqualTo(NonSerializableContextValue.ToStringValue));
        });
    }

    [Test]
    public void NonSerializableContextValue_WhenToStringThrows_FallsBackToTypeName_AndPayloadIsValidJson()
    {
        var contextData = new Dictionary<string, object> { { "endpoint", new ToStringThrowsContextValue() } };
        var sourceObject = new LogEventWireModelCollection(
            "myApplicationName",
            "guid",
            "hostname",
            new List<Label>(),
            new List<LogEventWireModel>
            {
                new LogEventWireModel(1, "TestMessage", "TestLevel", "TestSpanId", "TestTraceId", contextData)
            });

        var serialized = JsonConvert.SerializeObject(sourceObject, Formatting.None);

        Assert.DoesNotThrow(() => JToken.Parse(serialized), "log_event_data payload must be valid JSON");

        var attribute = JObject.Parse(serialized)["logs"][0]["attributes"]["context.endpoint"];
        Assert.Multiple(() =>
        {
            Assert.That(attribute.Type, Is.EqualTo(JTokenType.String), "value must be a quoted JSON string");
            Assert.That(attribute.Value<string>(), Is.EqualTo(typeof(ToStringThrowsContextValue).ToString()));
        });
    }
}
