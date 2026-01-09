// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Text;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.WireModels;
using Newtonsoft.Json;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.JsonConverters;

[TestFixture]
public class EventWireModelSerializerTests
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
        _configAutoResponder?.Dispose();
        _attribDefSvc?.Dispose();
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

    #region SpanEventWireModelSerializer Tests

    [Test]
    public void SpanEventWireModelSerializer_SerializesSpanWithoutLinksOrEvents()
    {
        // Arrange
        var attribValues = new SpanAttributeValueCollection();
        attribValues.Priority = 0.75f;
        _attribDefs.GetTypeAttribute(TypeAttributeValue.Span).TrySetDefault(attribValues);
        _attribDefs.DistributedTraceId.TrySetValue(attribValues, "trace-123");
        _attribDefs.Guid.TrySetValue(attribValues, "span-456");

        var serializer = new SpanEventWireModelSerializer();
        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));

        // Act
        serializer.WriteJson(writer, attribValues, JsonSerializer.CreateDefault());
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.Not.Null);
        Assert.That(actualJson, Does.Contain("\"type\":\"Span\""));
        Assert.That(actualJson, Does.Contain("\"traceId\":\"trace-123\""));
        Assert.That(actualJson, Does.Contain("\"guid\":\"span-456\""));
    }

    [Test]
    public void SpanEventWireModelSerializer_SerializesSpanWithLinks()
    {
        // Arrange
        var attribValues = new SpanAttributeValueCollection();
        attribValues.Priority = 0.5f;
        _attribDefs.GetTypeAttribute(TypeAttributeValue.Span).TrySetDefault(attribValues);
        _attribDefs.Guid.TrySetValue(attribValues, "span-main");

        // Add a span link
        var linkAttribValues = new SpanAttributeValueCollection();
        _attribDefs.GetTypeAttribute(TypeAttributeValue.SpanLink).TrySetDefault(linkAttribValues);
        _attribDefs.LinkedTraceId.TrySetValue(linkAttribValues, "linked-trace-123");
        _attribDefs.LinkedSpanId.TrySetValue(linkAttribValues, "linked-span-456");
        var spanLink = new SpanLinkWireModel(linkAttribValues);
        attribValues.Span.Links.Add(spanLink);

        var serializer = new SpanEventWireModelSerializer();
        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));

        // Act
        serializer.WriteJson(writer, attribValues, JsonSerializer.CreateDefault());
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.Not.Null);
        Assert.That(actualJson, Does.Contain("\"type\":\"Span\""));
        Assert.That(actualJson, Does.Contain("\"type\":\"SpanLink\""));
        Assert.That(actualJson, Does.Contain("\"linkedTraceId\":\"linked-trace-123\""));
        Assert.That(actualJson, Does.Contain("\"linkedSpanId\":\"linked-span-456\""));
    }

    [Test]
    public void SpanEventWireModelSerializer_SerializesSpanWithEvents()
    {
        // Arrange
        var attribValues = new SpanAttributeValueCollection();
        attribValues.Priority = 0.5f;
        _attribDefs.GetTypeAttribute(TypeAttributeValue.Span).TrySetDefault(attribValues);
        _attribDefs.Guid.TrySetValue(attribValues, "span-main");

        // Add a span event
        var eventAttribValues = new SpanAttributeValueCollection();
        _attribDefs.GetTypeAttribute(TypeAttributeValue.SpanEvent).TrySetDefault(eventAttribValues);
        _attribDefs.NameForSpan.TrySetValue(eventAttribValues, "test-event");
        _attribDefs.TraceIdForSpanData.TrySetValue(eventAttribValues, "trace-789");
        var spanEvent = new SpanEventEventWireModel(eventAttribValues);
        attribValues.Span.Events.Add(spanEvent);

        var serializer = new SpanEventWireModelSerializer();
        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));

        // Act
        serializer.WriteJson(writer, attribValues, JsonSerializer.CreateDefault());
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.Not.Null);
        Assert.That(actualJson, Does.Contain("\"type\":\"Span\""));
        Assert.That(actualJson, Does.Contain("\"type\":\"SpanEvent\""));
        Assert.That(actualJson, Does.Contain("\"name\":\"test-event\""));
        Assert.That(actualJson, Does.Contain("\"trace.id\":\"trace-789\""));
    }

    [Test]
    public void SpanEventWireModelSerializer_SerializesSpanWithMultipleLinks()
    {
        // Arrange
        var attribValues = new SpanAttributeValueCollection();
        attribValues.Priority = 0.5f;
        _attribDefs.GetTypeAttribute(TypeAttributeValue.Span).TrySetDefault(attribValues);

        // Add multiple span links
        for (int i = 0; i < 3; i++)
        {
            var linkAttribValues = new SpanAttributeValueCollection();
            _attribDefs.GetTypeAttribute(TypeAttributeValue.SpanLink).TrySetDefault(linkAttribValues);
            _attribDefs.LinkedTraceId.TrySetValue(linkAttribValues, $"trace-{i}");
            _attribDefs.LinkedSpanId.TrySetValue(linkAttribValues, $"span-{i}");
            var spanLink = new SpanLinkWireModel(linkAttribValues);
            attribValues.Span.Links.Add(spanLink);
        }

        var serializer = new SpanEventWireModelSerializer();
        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));

        // Act
        serializer.WriteJson(writer, attribValues, JsonSerializer.CreateDefault());
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.Not.Null);
        Assert.That(actualJson, Does.Contain("\"linkedTraceId\":\"trace-0\""));
        Assert.That(actualJson, Does.Contain("\"linkedTraceId\":\"trace-1\""));
        Assert.That(actualJson, Does.Contain("\"linkedTraceId\":\"trace-2\""));
    }

    [Test]
    public void SpanEventWireModelSerializer_SerializesSpanWithMultipleEvents()
    {
        // Arrange
        var attribValues = new SpanAttributeValueCollection();
        attribValues.Priority = 0.5f;
        _attribDefs.GetTypeAttribute(TypeAttributeValue.Span).TrySetDefault(attribValues);

        // Add multiple span events
        for (int i = 0; i < 3; i++)
        {
            var eventAttribValues = new SpanAttributeValueCollection();
            _attribDefs.GetTypeAttribute(TypeAttributeValue.SpanEvent).TrySetDefault(eventAttribValues);
            _attribDefs.NameForSpan.TrySetValue(eventAttribValues, $"event-{i}");
            var spanEvent = new SpanEventEventWireModel(eventAttribValues);
            attribValues.Span.Events.Add(spanEvent);
        }

        var serializer = new SpanEventWireModelSerializer();
        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));

        // Act
        serializer.WriteJson(writer, attribValues, JsonSerializer.CreateDefault());
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.Not.Null);
        Assert.That(actualJson, Does.Contain("\"name\":\"event-0\""));
        Assert.That(actualJson, Does.Contain("\"name\":\"event-1\""));
        Assert.That(actualJson, Does.Contain("\"name\":\"event-2\""));
    }

    [Test]
    public void SpanEventWireModelSerializer_SerializesSpanWithBothLinksAndEvents()
    {
        // Arrange
        var attribValues = new SpanAttributeValueCollection();
        attribValues.Priority = 0.5f;
        _attribDefs.GetTypeAttribute(TypeAttributeValue.Span).TrySetDefault(attribValues);
        _attribDefs.Guid.TrySetValue(attribValues, "main-span");

        // Add a span link
        var linkAttribValues = new SpanAttributeValueCollection();
        _attribDefs.GetTypeAttribute(TypeAttributeValue.SpanLink).TrySetDefault(linkAttribValues);
        _attribDefs.LinkedTraceId.TrySetValue(linkAttribValues, "link-trace");
        _attribDefs.LinkedSpanId.TrySetValue(linkAttribValues, "link-span");
        var spanLink = new SpanLinkWireModel(linkAttribValues);
        attribValues.Span.Links.Add(spanLink);

        // Add a span event
        var eventAttribValues = new SpanAttributeValueCollection();
        _attribDefs.GetTypeAttribute(TypeAttributeValue.SpanEvent).TrySetDefault(eventAttribValues);
        _attribDefs.NameForSpan.TrySetValue(eventAttribValues, "test-event");
        var spanEvent = new SpanEventEventWireModel(eventAttribValues);
        attribValues.Span.Events.Add(spanEvent);

        var serializer = new SpanEventWireModelSerializer();
        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));

        // Act
        serializer.WriteJson(writer, attribValues, JsonSerializer.CreateDefault());
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(actualJson, Does.Contain("\"type\":\"Span\""));
            Assert.That(actualJson, Does.Contain("\"type\":\"SpanLink\""));
            Assert.That(actualJson, Does.Contain("\"type\":\"SpanEvent\""));
            Assert.That(actualJson, Does.Contain("\"guid\":\"main-span\""));
            Assert.That(actualJson, Does.Contain("\"linkedTraceId\":\"link-trace\""));
            Assert.That(actualJson, Does.Contain("\"name\":\"test-event\""));
        });
    }

    [Test]
    public void SpanEventWireModelSerializer_SerializesLinkWithCustomAttributes()
    {
        // Arrange
        var attribValues = new SpanAttributeValueCollection();
        attribValues.Priority = 0.5f;
        _attribDefs.GetTypeAttribute(TypeAttributeValue.Span).TrySetDefault(attribValues);

        // Add a span link with custom attributes
        var linkAttribValues = new SpanAttributeValueCollection();
        _attribDefs.GetTypeAttribute(TypeAttributeValue.SpanLink).TrySetDefault(linkAttribValues);
        _attribDefs.LinkedTraceId.TrySetValue(linkAttribValues, "trace-abc");
        _attribDefs.LinkedSpanId.TrySetValue(linkAttribValues, "span-def");
        _attribDefs.GetCustomAttributeForSpan("link.custom").TrySetValue(linkAttribValues, "custom-value");
        var spanLink = new SpanLinkWireModel(linkAttribValues);
        attribValues.Span.Links.Add(spanLink);

        var serializer = new SpanEventWireModelSerializer();
        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));

        // Act
        serializer.WriteJson(writer, attribValues, JsonSerializer.CreateDefault());
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.Not.Null);
        Assert.That(actualJson, Does.Contain("\"link.custom\":\"custom-value\""));
    }

    [Test]
    public void SpanEventWireModelSerializer_SerializesEventWithCustomAttributes()
    {
        // Arrange
        var attribValues = new SpanAttributeValueCollection();
        attribValues.Priority = 0.5f;
        _attribDefs.GetTypeAttribute(TypeAttributeValue.Span).TrySetDefault(attribValues);

        // Add a span event with custom attributes
        var eventAttribValues = new SpanAttributeValueCollection();
        _attribDefs.GetTypeAttribute(TypeAttributeValue.SpanEvent).TrySetDefault(eventAttribValues);
        _attribDefs.NameForSpan.TrySetValue(eventAttribValues, "custom-event");
        _attribDefs.GetCustomAttributeForSpan("event.custom").TrySetValue(eventAttribValues, 123);
        var spanEvent = new SpanEventEventWireModel(eventAttribValues);
        attribValues.Span.Events.Add(spanEvent);

        var serializer = new SpanEventWireModelSerializer();
        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));

        // Act
        serializer.WriteJson(writer, attribValues, JsonSerializer.CreateDefault());
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.Not.Null);
        Assert.That(actualJson, Does.Contain("\"event.custom\":123"));
    }

    [Test]
    public void SpanEventWireModelSerializer_ReadJson_ThrowsNotImplementedException()
    {
        // Arrange
        var serializer = new SpanEventWireModelSerializer();
        var reader = new JsonTextReader(new StringReader("[]"));

        // Act & Assert
        Assert.Throws<NotImplementedException>(() => 
            serializer.ReadJson(reader, typeof(ISpanEventWireModel), null, false, JsonSerializer.CreateDefault()));
    }

    #endregion

    #region Integration Tests

    [Test]
    public void SpanEventWireModelSerializer_ProducesValidJsonStructure()
    {
        // Arrange
        var attribValues = new SpanAttributeValueCollection();
        attribValues.Priority = 0.5f;
        _attribDefs.GetTypeAttribute(TypeAttributeValue.Span).TrySetDefault(attribValues);
        _attribDefs.DistributedTraceId.TrySetValue(attribValues, "trace-123");

        var serializer = new SpanEventWireModelSerializer();
        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));

        // Act
        serializer.WriteJson(writer, attribValues, JsonSerializer.CreateDefault());
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert - Should be valid JSON that can be deserialized
        Assert.DoesNotThrow(() => JsonConvert.DeserializeObject(actualJson));
    }

    [Test]
    public void EventWireModelSerializer_ProducesValidJsonStructure()
    {
        // Arrange
        var attribValues = new AttributeValueCollection(AttributeDestinations.TransactionEvent);
        _attribDefs.GetTypeAttribute(TypeAttributeValue.Transaction).TrySetDefault(attribValues);
        var wireModel = new TransactionEventWireModel(attribValues, false, 0.5f);

        var serializer = new EventWireModelSerializer();
        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));

        // Act
        serializer.WriteJson(writer, wireModel, JsonSerializer.CreateDefault());
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert - Should be valid JSON that can be deserialized
        Assert.DoesNotThrow(() => JsonConvert.DeserializeObject(actualJson));
    }

    #endregion
}
