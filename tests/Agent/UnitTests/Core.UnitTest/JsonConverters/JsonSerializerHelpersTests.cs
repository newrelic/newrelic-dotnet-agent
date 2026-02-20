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

    #region WriteValue Method Coverage Tests

    [Test]
    public void WriteObjectCollection_WritesDoubleCorrectly()
    {
        // Arrange
        var collection = new Dictionary<string, object>
        {
            { "temperature", 98.6 },
            { "pi", 3.14159 }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"temperature\":98.6,\"pi\":3.14159}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_WritesFloatCorrectly()
    {
        // Arrange
        var collection = new Dictionary<string, object>
        {
            { "value", 123.45f },
            { "negative", -67.89f }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"value\":123.45,\"negative\":-67.89}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_WritesDecimalCorrectly()
    {
        // Arrange
        var collection = new Dictionary<string, object>
        {
            { "price", 99.99m },
            { "tax", 0.08m }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"price\":99.99,\"tax\":0.08}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_WritesCharCorrectly()
    {
        // Arrange
        var collection = new Dictionary<string, object>
        {
            { "initial", 'J' },
            { "grade", 'A' }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"initial\":\"J\",\"grade\":\"A\"}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_WritesUnsignedTypesCorrectly()
    {
        // Arrange
        var collection = new Dictionary<string, object>
        {
            { "ushortVal", (ushort)65535 },
            { "uintVal", (uint)4294967295 },
            { "ulongVal", (ulong)18446744073709551615 }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"ushortVal\":65535,\"uintVal\":4294967295,\"ulongVal\":18446744073709551615}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_WritesSignedTypesCorrectly()
    {
        // Arrange
        var collection = new Dictionary<string, object>
        {
            { "shortVal", (short)-32768 },
            { "sbyteVal", (sbyte)-128 },
            { "byteVal", (byte)255 }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"shortVal\":-32768,\"sbyteVal\":-128,\"byteVal\":255}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_WritesLongCorrectly()
    {
        // Arrange
        var collection = new Dictionary<string, object>
        {
            { "bigNumber", 9223372036854775807L },
            { "negativeNumber", -9223372036854775808L }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"bigNumber\":9223372036854775807,\"negativeNumber\":-9223372036854775808}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_HandlesNonSerializableObjectWithToString()
    {
        // Arrange - Use a custom object that JsonWriter cannot serialize directly
        var customObject = new CustomNonSerializableObject("test-value");
        var collection = new Dictionary<string, object>
        {
            { "customObj", customObject }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"customObj\":\"CustomObject: test-value\"}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_HandlesArrayWithNonSerializableObjects()
    {
        // Arrange
        var customObjects = new[]
        {
            new CustomNonSerializableObject("first"),
            new CustomNonSerializableObject("second")
        };
        var collection = new Dictionary<string, object>
        {
            { "customArray", customObjects }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"customArray\":[\"CustomObject: first\",\"CustomObject: second\"]}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_HandlesMixedNumericTypesInArray()
    {
        // Arrange
        var mixedArray = new object[] { 1, 2.5, 3.0f, 4m, (byte)5, (short)6, 7L };
        var collection = new Dictionary<string, object>
        {
            { "mixedNumbers", mixedArray }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"mixedNumbers\":[1,2.5,3.0,4.0,5,6,7]}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_HandlesCharArray()
    {
        // Arrange
        var charArray = new[] { 'A', 'B', 'C' };
        var collection = new Dictionary<string, object>
        {
            { "letters", charArray }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"letters\":[\"A\",\"B\",\"C\"]}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    [Test]
    public void WriteObjectCollection_HandlesBoolArrayCorrectly()
    {
        // Arrange
        var boolArray = new[] { true, false, true, false };
        var collection = new Dictionary<string, object>
        {
            { "flags", boolArray }
        };

        var stringBuilder = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(stringBuilder));
        var expectedJson = "{\"flags\":[true,false,true,false]}";

        // Act
        JsonSerializerHelpers.WriteObjectCollection(writer, collection);
        writer.Flush();
        var actualJson = stringBuilder.ToString();

        // Assert
        Assert.That(actualJson, Is.EqualTo(expectedJson));
    }

    #endregion

    // Helper class for testing non-serializable objects
    private class CustomNonSerializableObject
    {
        private readonly string _value;

        public CustomNonSerializableObject(string value)
        {
            _value = value;
        }

        public override string ToString()
        {
            return $"CustomObject: {_value}";
        }
    }
}