// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.IO;
using System.Text;
using NewRelic.Agent.Core.Attributes;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.JsonConverters.Tests;

[TestFixture]
public class JsonSerializerHelpersTests
{
    [Test]
    public void WriteCollection_WritesCorrectJson()
    {

        var attribValues = new List<IAttributeValue>
        {
            new AttributeValue(new AttributeDefinition("name", AttributeClassification.AgentAttributes, new Dictionary<AttributeDestinations, bool>()
            {
                {AttributeDestinations.All, true }
            })) { Value = "John" },
            new AttributeValue(new AttributeDefinition("age", AttributeClassification.AgentAttributes, new Dictionary<AttributeDestinations, bool>()
            {
                {AttributeDestinations.All, true }
            })) { Value = 30 },
            new AttributeValue(new AttributeDefinition("isStudent", AttributeClassification.AgentAttributes, new Dictionary<AttributeDestinations, bool>()
            {
                {AttributeDestinations.All, true }
            })) { Value = true },
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"age\":30,\"isStudent\":true,\"name\":\"John\"}";

        // Act
        JsonSerializerHelpers.WriteCollection(writer, attribValues);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteCollection_WritesEmptyJson_WhenAttribValuesIsNull()
    {
        // Arrange
        IEnumerable<IAttributeValue> attribValues = null;

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{}";

        // Act
        JsonSerializerHelpers.WriteCollection(writer, attribValues);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteCollection_WritesEmptyJson_WhenAllAttribValuesAreNull()
    {
        // Arrange
        var attribValues = new List<IAttributeValue>
        {
            new AttributeValue(new AttributeDefinition("name", AttributeClassification.AgentAttributes, new Dictionary<AttributeDestinations, bool>()
            {
                {AttributeDestinations.All, true }
            })) { Value = null },
            new AttributeValue(new AttributeDefinition("age", AttributeClassification.AgentAttributes, new Dictionary<AttributeDestinations, bool>()
            {
                {AttributeDestinations.All, true }
            })) { Value = null },
            new AttributeValue(new AttributeDefinition("isStudent", AttributeClassification.AgentAttributes, new Dictionary<AttributeDestinations, bool>()
            {
                {AttributeDestinations.All, true }
            })) { Value = null },
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{}";

        // Act
        JsonSerializerHelpers.WriteCollection(writer, attribValues);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_WritesCorrectJson()
    {
        // Arrange
        var collection = new Dictionary<string, object>
        {
            { "name", "John" },
            { "age", 30 },
            { "isStudent", true }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"name\":\"John\",\"age\":30,\"isStudent\":true}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_WritesEmptyJson_WhenCollectionIsEmpty()
    {
        // Arrange
        IEnumerable<KeyValuePair<string, object>> collection = [];

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_WritesEmptyJson_WhenAllValuesInCollectionAreNull()
    {
        // Arrange
        var collection = new Dictionary<string, object>
        {
            { "name", null },
            { "age", null },
            { "isStudent", null }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_WritesStringArrayCorrectly()
    {
        // Arrange
        var collection = new Dictionary<string, object>
        {
            { "colors", new[] { "red", "green", "blue" } }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"colors\":[\"red\",\"green\",\"blue\"]}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_WritesIntArrayCorrectly()
    {
        // Arrange
        var collection = new Dictionary<string, object>
        {
            { "numbers", new[] { 1, 2, 3, 4, 5 } }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"numbers\":[1,2,3,4,5]}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_SkipsEmptyArray()
    {
        // Arrange
        var collection = new Dictionary<string, object>
        {
            { "empty", new string[] { } }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_SkipsNullElementsInArray()
    {
        // Arrange
        var collection = new Dictionary<string, object>
        {
            { "withNulls", new object[] { "first", null, "third", null } }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"withNulls\":[\"first\",\"third\"]}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_SkipsArrayWhenAllElementsAreNull()
    {
        // Arrange
        var collection = new Dictionary<string, object>
        {
            { "allNulls", new object[] { null, null, null } }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_WritesArrayWithMixedNullAndNonNullElements()
    {
        // Arrange
        var collection = new Dictionary<string, object>
        {
            { "withSomeNulls", new object[] { "first", null, "third", null } },
            { "allNulls", new object[] { null, null } },
            { "valid", new[] { 1, 2, 3 } }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"withSomeNulls\":[\"first\",\"third\"],\"valid\":[1,2,3]}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_WritesListCorrectly()
    {
        // Arrange
        var collection = new Dictionary<string, object>
        {
            { "list", new List<string> { "first", "second", "third" } }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"list\":[\"first\",\"second\",\"third\"]}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_TreatsStringAsString_NotAsCharArray()
    {
        // Arrange
        var collection = new Dictionary<string, object>
        {
            { "text", "hello" }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"text\":\"hello\"}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteCollection_WritesArrayAttributeCorrectly()
    {
        // Arrange
        var attribValues = new List<IAttributeValue>
        {
            new AttributeValue(new AttributeDefinition("tags", AttributeClassification.AgentAttributes, new Dictionary<AttributeDestinations, bool>()
            {
                {AttributeDestinations.All, true }
            })) { Value = new[] { "web", "api", "json" } }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"tags\":[\"web\",\"api\",\"json\"]}";

        // Act
        JsonSerializerHelpers.WriteCollection(writer, attribValues);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

}