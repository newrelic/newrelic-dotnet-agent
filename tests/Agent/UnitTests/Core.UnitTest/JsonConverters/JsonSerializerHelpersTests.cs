// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;
using NewRelic.Agent.Core.Attributes;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace NewRelic.Agent.Core.JsonConverters.Tests
{
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

    }
}
