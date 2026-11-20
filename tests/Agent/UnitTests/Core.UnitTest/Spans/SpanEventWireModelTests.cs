// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.TestUtilities;
using Newtonsoft.Json;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Spans;

[TestFixture]
class SpanEventWireModelTests
{
    private ConfigurationAutoResponder _configAutoResponder;

    private IAttributeDefinitionService _attribDefSvc;

    private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;

    [SetUp]
    public void SetUp()
    {
        _configAutoResponder = new ConfigurationAutoResponder(GetConfiguration());
        _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
    }

    [TearDown]
    public void TearDown()
    {
        _configAutoResponder.Dispose();
        _attribDefSvc.Dispose();
    }

    private IConfiguration GetConfiguration()
    {
            
        var config = Mock.Create<IConfiguration>();

        Mock.Arrange(() => config.ConfigurationVersion).Returns(int.MaxValue);
        Mock.Arrange(() => config.SpanEventsEnabled).Returns(true);
        Mock.Arrange(() => config.CaptureAttributes).Returns(true);
        Mock.Arrange(() => config.DistributedTracingEnabled).Returns(true);

        return config;
    }

    [Test]
    public void SpanEventWireModelTests_Serialization()
    {
        const float priority = 1.975676f;
        var duration = TimeSpan.FromSeconds(4.81);

        var expectedSerialization = new Dictionary<string, object>[3]
        {
            new Dictionary<string,object>
            {
                { "type", "Span"},
                { "priority", priority},
                { "traceId", "ed5bbf27f28ebef3" },
                { "duration", duration.TotalSeconds }
            },
            new Dictionary<string, object>
            {

            },
            new Dictionary<string, object>
            {
                { "http.request.method","GET" }
            }
        };

        var spanEventWireModel = new SpanAttributeValueCollection();
        spanEventWireModel.Priority = priority;
        _attribDefs.GetTypeAttribute(TypeAttributeValue.Span).TrySetDefault(spanEventWireModel);
        _attribDefs.Priority.TrySetValue(spanEventWireModel, priority);
        _attribDefs.Duration.TrySetValue(spanEventWireModel, duration);
        _attribDefs.DistributedTraceId.TrySetValue(spanEventWireModel, "ed5bbf27f28ebef3");
        _attribDefs.HttpRequestMethod.TrySetValue(spanEventWireModel, "GET");

        var serialized = JsonConvert.SerializeObject(spanEventWireModel);
        Assert.That(serialized, Is.Not.Null);

        var deserialized = JsonConvert.DeserializeObject<Dictionary<string, object>[]>(serialized);
        Assert.That(deserialized, Is.Not.Null);

        AttributeComparer.CompareDictionaries(expectedSerialization, deserialized);
    }

    [Test]
    public void SpanEventWireModelTests_MultipleEvents_Serialization()
    {
        const float priority = 1.975676f;
        var duration = TimeSpan.FromSeconds(4.81);

        var expectedSerialization = new List<Dictionary<string, object>[]>
        {
            new[] {
                new Dictionary<string,object>
                {
                    { "type", "Span"},
                    { "priority", priority},
                    { "traceId", "ed5bbf27f28ebef3" },
                    { "duration", duration.TotalSeconds }
                },
                new Dictionary<string, object>
                {

                },
                new Dictionary<string, object>
                {
                    { "http.request.method","GET" }
                }
            },
            new[] {
                new Dictionary<string,object>
                {
                    { "type", "Span"},
                    { "priority", priority - 1},
                    { "traceId", "fa5bbf27f28ebef3" },
                    { "duration", duration.TotalSeconds - 1}
                },
                new Dictionary<string, object>
                {

                },
                new Dictionary<string, object>
                {
                    { "http.request.method","POST" }
                }
            }
        };

        var spanEvents = new[]
        {
            CreateSpanEvent(priority, duration, "ed5bbf27f28ebef3", "GET"),
            CreateSpanEvent(priority -1, duration.Subtract(TimeSpan.FromSeconds(1)), "fa5bbf27f28ebef3", "POST")
        };

        var serialized = JsonConvert.SerializeObject(spanEvents);
        Assert.That(serialized, Is.Not.Null);

        var deserialized = JsonConvert.DeserializeObject<List<Dictionary<string, object>[]>>(serialized);
        Assert.That(deserialized, Is.Not.Null);

        Assert.That(deserialized, Has.Count.EqualTo(expectedSerialization.Count));
        AttributeComparer.CompareDictionaries(expectedSerialization[0], deserialized[0]);
        AttributeComparer.CompareDictionaries(expectedSerialization[1], deserialized[1]);
    }

    private ISpanEventWireModel CreateSpanEvent(float priority, TimeSpan duration, string traceId, string httpRequestMethod)
    {
        var wireModel = new SpanAttributeValueCollection();
        wireModel.Priority = priority;
        _attribDefs.GetTypeAttribute(TypeAttributeValue.Span).TrySetDefault(wireModel);
        _attribDefs.Priority.TrySetValue(wireModel, priority);
        _attribDefs.Duration.TrySetValue(wireModel, duration);
        _attribDefs.DistributedTraceId.TrySetValue(wireModel, traceId);
        _attribDefs.HttpRequestMethod.TrySetValue(wireModel, httpRequestMethod);

        return wireModel;
    }

    #region SpanLinkWireModel Tests

    [Test]
    public void SpanLinkWireModel_Constructor_CreatesInstanceWithCorrectDestination()
    {
        // Arrange
        var attribValues = new SpanAttributeValueCollection();
        _attribDefs.GetTypeAttribute(TypeAttributeValue.SpanLink).TrySetDefault(attribValues);

        // Act
        var spanLink = new SpanLinkWireModel(attribValues);

        // Assert
        Assert.That(spanLink, Is.Not.Null);
        Assert.That(spanLink.IntrinsicAttributes(), Is.Not.Null);
    }

    [Test]
    public void SpanLinkWireModel_Serialization_WithBasicAttributes()
    {
        // Arrange
        var attribValues = new SpanAttributeValueCollection();
        _attribDefs.GetTypeAttribute(TypeAttributeValue.SpanLink).TrySetDefault(attribValues);
        _attribDefs.TraceIdForSpanData.TrySetValue(attribValues, "trace-123");
        _attribDefs.SpanIdForSpanLink.TrySetValue(attribValues, "span-456");
        _attribDefs.LinkedTraceId.TrySetValue(attribValues, "linked-trace-789");
        _attribDefs.LinkedSpanId.TrySetValue(attribValues, "linked-span-012");

        var spanLink = new SpanLinkWireModel(attribValues);

        // Act
        var serialized = JsonConvert.SerializeObject(spanLink);
        var deserialized = JsonConvert.DeserializeObject<Dictionary<string, object>[]>(serialized);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized[0]["type"], Is.EqualTo("SpanLink"));
        Assert.That(deserialized[0]["trace.id"], Is.EqualTo("trace-123"));
        Assert.That(deserialized[0]["id"], Is.EqualTo("span-456"));
        Assert.That(deserialized[0]["linkedTraceId"], Is.EqualTo("linked-trace-789"));
        Assert.That(deserialized[0]["linkedSpanId"], Is.EqualTo("linked-span-012"));
    }

    [Test]
    public void SpanLinkWireModel_Serialization_WithCustomAttributes()
    {
        // Arrange
        var attribValues = new SpanAttributeValueCollection();
        _attribDefs.GetTypeAttribute(TypeAttributeValue.SpanLink).TrySetDefault(attribValues);
        _attribDefs.TraceIdForSpanData.TrySetValue(attribValues, "trace-abc");
        _attribDefs.SpanIdForSpanLink.TrySetValue(attribValues, "span-def");
        _attribDefs.LinkedTraceId.TrySetValue(attribValues, "linked-trace-ghi");
        _attribDefs.LinkedSpanId.TrySetValue(attribValues, "linked-span-jkl");
        _attribDefs.SpanCustomAttributes.TrySetValue(attribValues, "link.attr1", "value1");
        _attribDefs.SpanCustomAttributes.TrySetValue(attribValues, "link.attr2", 42);

        var spanLink = new SpanLinkWireModel(attribValues);

        // Act
        var serialized = JsonConvert.SerializeObject(spanLink);
        var deserialized = JsonConvert.DeserializeObject<Dictionary<string, object>[]>(serialized);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized[0]["type"], Is.EqualTo("SpanLink"));
        Assert.That(deserialized[0]["trace.id"], Is.EqualTo("trace-abc"));
        Assert.That(deserialized[0]["linkedTraceId"], Is.EqualTo("linked-trace-ghi"));
        Assert.That(deserialized[0]["linkedSpanId"], Is.EqualTo("linked-span-jkl"));
        Assert.That(deserialized[1]["link.attr1"], Is.EqualTo("value1"));
        Assert.That(deserialized[1]["link.attr2"], Is.EqualTo(42L)); // JSON deserializes as long
    }

    [Test]
    public void SpanLinkWireModel_Serialization_WithTimestamp()
    {
        // Arrange
        var timestamp = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);
        var attribValues = new SpanAttributeValueCollection();
        _attribDefs.GetTypeAttribute(TypeAttributeValue.SpanLink).TrySetDefault(attribValues);
        _attribDefs.Timestamp.TrySetValue(attribValues, timestamp);
        _attribDefs.LinkedTraceId.TrySetValue(attribValues, "linked-trace");
        _attribDefs.LinkedSpanId.TrySetValue(attribValues, "linked-span");

        var spanLink = new SpanLinkWireModel(attribValues);

        // Act
        var serialized = JsonConvert.SerializeObject(spanLink);
        var deserialized = JsonConvert.DeserializeObject<Dictionary<string, object>[]>(serialized);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized[0].ContainsKey("timestamp"), Is.True);
    }

    #endregion

    #region SpanEventEventWireModel Tests

    [Test]
    public void SpanEventEventWireModel_Constructor_CreatesInstanceWithCorrectDestination()
    {
        // Arrange
        var attribValues = new SpanAttributeValueCollection();
        _attribDefs.GetTypeAttribute(TypeAttributeValue.SpanEvent).TrySetDefault(attribValues);

        // Act
        var spanEvent = new SpanEventEventWireModel(attribValues);

        // Assert
        Assert.That(spanEvent, Is.Not.Null);
        Assert.That(spanEvent.IntrinsicAttributes(), Is.Not.Null);
    }

    [Test]
    public void SpanEventEventWireModel_Serialization_WithBasicAttributes()
    {
        // Arrange
        var timestamp = new DateTime(2024, 2, 20, 14, 25, 30, DateTimeKind.Utc);
        var attribValues = new SpanAttributeValueCollection();
        _attribDefs.GetTypeAttribute(TypeAttributeValue.SpanEvent).TrySetDefault(attribValues);
        _attribDefs.NameForSpan.TrySetValue(attribValues, "test-event");
        _attribDefs.TraceIdForSpanData.TrySetValue(attribValues, "trace-123");
        _attribDefs.SpanIdForSpanEvent.TrySetValue(attribValues, "span-456");
        _attribDefs.Timestamp.TrySetValue(attribValues, timestamp);

        var spanEvent = new SpanEventEventWireModel(attribValues);

        // Act
        var serialized = JsonConvert.SerializeObject(spanEvent);
        var deserialized = JsonConvert.DeserializeObject<Dictionary<string, object>[]>(serialized);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized[0]["type"], Is.EqualTo("SpanEvent"));
        Assert.That(deserialized[0]["name"], Is.EqualTo("test-event"));
        Assert.That(deserialized[0]["trace.id"], Is.EqualTo("trace-123"));
        Assert.That(deserialized[0]["span.id"], Is.EqualTo("span-456"));
        Assert.That(deserialized[0].ContainsKey("timestamp"), Is.True);
    }

    [Test]
    public void SpanEventEventWireModel_Serialization_WithCustomAttributes()
    {
        // Arrange
        var attribValues = new SpanAttributeValueCollection();
        _attribDefs.GetTypeAttribute(TypeAttributeValue.SpanEvent).TrySetDefault(attribValues);
        _attribDefs.NameForSpan.TrySetValue(attribValues, "custom-event");
        _attribDefs.TraceIdForSpanData.TrySetValue(attribValues, "trace-abc");
        _attribDefs.SpanIdForSpanEvent.TrySetValue(attribValues, "span-def");
        _attribDefs.SpanCustomAttributes.TrySetValue(attribValues, "event.attr1", "event-value");
        _attribDefs.SpanCustomAttributes.TrySetValue(attribValues, "event.attr2", 99);
        _attribDefs.SpanCustomAttributes.TrySetValue(attribValues, "event.attr3", true);

        var spanEvent = new SpanEventEventWireModel(attribValues);

        // Act
        var serialized = JsonConvert.SerializeObject(spanEvent);
        var deserialized = JsonConvert.DeserializeObject<Dictionary<string, object>[]>(serialized);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized[0]["type"], Is.EqualTo("SpanEvent"));
        Assert.That(deserialized[0]["name"], Is.EqualTo("custom-event"));
        Assert.That(deserialized[1]["event.attr1"], Is.EqualTo("event-value"));
        Assert.That(deserialized[1]["event.attr2"], Is.EqualTo(99L)); // JSON deserializes as long
        Assert.That(deserialized[1]["event.attr3"], Is.EqualTo(true));
    }

    #endregion

    private static AttributeDefinition CreateFamilyDefinition()
    {
        return new AttributeDefinition("test.family.*", AttributeClassification.UserAttributes, new Dictionary<AttributeDestinations, bool>());
    }

    private static ResolvedAttributeName Resolve(string name, AttributeDestinations destinations = AttributeDestinations.SpanEvent)
    {
        return new ResolvedAttributeName(name, AttributeClassification.UserAttributes, destinations);
    }

    [TestCase("traceId")]
    [TestCase("guid")]
    public void SpanCollection_TraceIdAndGuidSwitch_ReadsValueName(string name)
    {
        var collection = new SpanAttributeValueCollection();

        collection.TrySetValue(CreateFamilyDefinition(), Resolve(name), "abc123");

        Assert.Multiple(() =>
        {
            Assert.That(collection.Span.TraceId, Is.EqualTo(name == "traceId" ? "abc123" : string.Empty));
            Assert.That(collection.Span.SpanId, Is.EqualTo(name == "guid" ? "abc123" : null));
        });
    }

    [Test]
    public void SpanCollection_FamilySet_KeysByName()
    {
        var collection = new SpanAttributeValueCollection();
        var family = CreateFamilyDefinition();

        var first = collection.TrySetValue(family, Resolve("foo"), "one");
        var second = collection.TrySetValue(family, Resolve("bar"), "two");
        var overwrite = collection.TrySetValue(family, Resolve("foo"), "three");

        var values = collection.GetAttributeValuesDic(AttributeClassification.UserAttributes);
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(second, Is.True);
            Assert.That(overwrite, Is.False);
            Assert.That(collection.Count, Is.EqualTo(2));
            Assert.That(values["foo"], Is.EqualTo("three"));
            Assert.That(values["bar"], Is.EqualTo("two"));
        });
    }

    [Test]
    public void SpanCollection_FamilySet_UserLimit_UsesName()
    {
        var collection = new SpanAttributeValueCollection();
        var family = CreateFamilyDefinition();
        for (var i = 1; i <= 64; i++)
        {
            collection.TrySetValue(family, Resolve($"name{i}"), i);
        }

        var rejected = collection.TrySetValue(family, Resolve("name65"), 65);
        collection.TrySetValue(family, Resolve("name1"), 1000);

        var values = collection.GetAttributeValuesDic(AttributeClassification.UserAttributes);
        Assert.Multiple(() =>
        {
            Assert.That(rejected, Is.False);
            Assert.That(values, Has.Count.EqualTo(64));
            Assert.That(values["name1"], Is.EqualTo(1000L));
        });
    }

    [Test]
    public void SpanCollection_RemoveItems_RemovesByValueName()
    {
        var collection = new SpanAttributeValueCollection();
        var family = CreateFamilyDefinition();
        collection.TrySetValue(family, Resolve("foo"), null);
        collection.TrySetValue(family, Resolve("bar"), "kept");

        collection.MakeImmutable();

        var values = collection.GetAttributeValuesDic(AttributeClassification.UserAttributes);
        Assert.Multiple(() =>
        {
            Assert.That(values, Has.Count.EqualTo(1));
            Assert.That(values["bar"], Is.EqualTo("kept"));
        });
    }

    [Test]
    public void CopyFallback_FamilyValue_KeepsNameAndDestinations()
    {
        var source = new SpanAttributeValueCollection();
        var family = CreateFamilyDefinition();
        source.TrySetValue(family, Resolve("foo", AttributeDestinations.SpanEvent), "one");
        var destination = new AttributeValueCollection(AttributeDestinations.SpanEvent);

        destination.AddRange(source);

        var copied = destination.GetAttributeValues(AttributeClassification.UserAttributes).Single();
        Assert.Multiple(() =>
        {
            Assert.That(copied.Name, Is.EqualTo("foo"));
            Assert.That(copied.AttributeDestinations, Is.EqualTo(AttributeDestinations.SpanEvent));
            Assert.That(copied.Key.Equals(new AttributeKey(family.Guid, "foo")), Is.True);
            Assert.That(copied.Value, Is.EqualTo("one"));
        });
    }

    [Test]
    public void CoreCollectionCopiedIntoSpanCollection_KeepsOnlySpanEventFamilyValues()
    {
        var family = CreateFamilyDefinition();
        var source = new AttributeValueCollection(AttributeDestinations.SpanEvent, AttributeDestinations.TransactionEvent);
        source.TrySetValue(family, Resolve("foo", AttributeDestinations.SpanEvent), "one");
        source.TrySetValue(family, Resolve("bar", AttributeDestinations.TransactionEvent), "two");
        var destination = new SpanAttributeValueCollection();

        destination.AddRange(source);

        var copied = destination.GetAttributeValues(AttributeClassification.UserAttributes).Single();
        Assert.Multiple(() =>
        {
            Assert.That(destination.Count, Is.EqualTo(1));
            Assert.That(copied.Name, Is.EqualTo("foo"));
            Assert.That(copied.AttributeDestinations, Is.EqualTo(AttributeDestinations.SpanEvent));
            Assert.That(copied.Value, Is.EqualTo("one"));
        });
    }

    [Test]
    public void CopyFallback_FamilyValue_TwoNames_TwoValues()
    {
        var source = new SpanAttributeValueCollection();
        var family = CreateFamilyDefinition();
        source.TrySetValue(family, Resolve("foo"), "one");
        source.TrySetValue(family, Resolve("bar"), "two");
        var destination = new AttributeValueCollection(AttributeDestinations.SpanEvent);

        destination.AddRange(source);

        var copied = destination.GetAttributeValuesDic(AttributeClassification.UserAttributes);
        Assert.Multiple(() =>
        {
            Assert.That(copied, Has.Count.EqualTo(2));
            Assert.That(copied["foo"], Is.EqualTo("one"));
            Assert.That(copied["bar"], Is.EqualTo("two"));
        });
    }

    [Test]
    public void CopyFallback_FamilyValue_NotForTargetDestination_IsDropped()
    {
        var source = new SpanAttributeValueCollection();
        source.TrySetValue(CreateFamilyDefinition(), Resolve("foo", AttributeDestinations.TransactionEvent), "one");
        var destination = new AttributeValueCollection(AttributeDestinations.SpanEvent);

        destination.AddRange(source);

        Assert.That(destination.Count, Is.EqualTo(0));
    }

    [Test]
    public void CopyFallback_FamilyValueWithoutValue_IsDroppedAndNotCounted()
    {
        var source = new SpanAttributeValueCollection();
        source.TrySetValue(CreateFamilyDefinition(), Resolve("foo"), null);
        var destination = new AttributeValueCollection(AttributeDestinations.SpanEvent);

        destination.AddRange(source);

        Assert.That(destination.Count, Is.EqualTo(0));
    }

    [Test]
    public void CopyFallback_StaticValue_KeepsNullKeyName()
    {
        var source = new SpanAttributeValueCollection();
        _attribDefs.HttpRequestMethod.TrySetValue(source, "GET");
        var destination = new AttributeValueCollection(AttributeDestinations.SpanEvent);

        destination.AddRange(source);

        var copied = destination.GetAttributeValues(AttributeClassification.AgentAttributes).Single();
        Assert.Multiple(() =>
        {
            Assert.That(copied.Key.Name, Is.Null);
            Assert.That(copied.Key.DefinitionGuid, Is.EqualTo(_attribDefs.HttpRequestMethod.Guid));
            Assert.That(copied.Name, Is.EqualTo(_attribDefs.HttpRequestMethod.Name));
            Assert.That(copied.AttributeDestinations, Is.EqualTo(_attribDefs.HttpRequestMethod.AttributeDestinations));
        });
    }
}
