// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.OpenTelemetryBridge;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.OpenTelemetryBridge
{
    [TestFixture]
    public class TagHelpersTests
    {
        [Test]
        public void TryGetTag_Should_Return_True_And_Value_When_Key_Exists()
        {
            // Arrange
            var tags = new Dictionary<string, object> { { "key1", "value1" } };
            var keys = new[] { "key1" };

            // Act
            var result = tags.TryGetTag(keys, out string value);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(value, Is.EqualTo("value1"));
            Assert.That(tags, Has.Count.EqualTo(1)); // Should not remove
        }

        [Test]
        public void TryGetTag_Should_Return_True_And_Value_When_First_Of_Multiple_Keys_Exists()
        {
            // Arrange
            var tags = new Dictionary<string, object> { { "key1", "value1" }, { "key2", "value2" } };
            var keys = new[] { "key1", "key2" };

            // Act
            var result = tags.TryGetTag(keys, out string value);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(value, Is.EqualTo("value1"));
            Assert.That(tags, Has.Count.EqualTo(2));
        }

        [Test]
        public void TryGetTag_Should_Return_True_And_Value_When_Second_Of_Multiple_Keys_Exists()
        {
            // Arrange
            var tags = new Dictionary<string, object> { { "key2", "value2" } };
            var keys = new[] { "key1", "key2" };

            // Act
            var result = tags.TryGetTag(keys, out string value);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(value, Is.EqualTo("value2"));
            Assert.That(tags, Has.Count.EqualTo(1));
        }

        [Test]
        public void TryGetTag_Should_Return_False_When_Key_Does_Not_Exist()
        {
            // Arrange
            var tags = new Dictionary<string, object> { { "key1", "value1" } };
            var keys = new[] { "nonexistent" };

            // Act
            var result = tags.TryGetTag(keys, out string value);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(value, Is.Null);
        }

        [Test]
        public void TryGetTag_Should_Return_False_When_Value_Is_Wrong_Type()
        {
            // Arrange
            var tags = new Dictionary<string, object> { { "key1", 123 } };
            var keys = new[] { "key1" };

            // Act
            var result = tags.TryGetTag(keys, out string value);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(value, Is.Null);
        }

        [Test]
        public void TryGetAndRemoveTag_Should_Return_True_And_Value_And_Remove_Tag()
        {
            // Arrange
            var tags = new Dictionary<string, object> { { "key1", "value1" } };
            var keys = new[] { "key1" };

            // Act
            var result = tags.TryGetAndRemoveTag(keys, out string value);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(value, Is.EqualTo("value1"));
            Assert.That(tags, Is.Empty);
        }

        [Test]
        public void TryGetAndRemoveTag_Should_Return_True_And_Value_And_Remove_All_Keys()
        {
            // Arrange
            var tags = new Dictionary<string, object> { { "key1", "value1" }, { "key2", "value2" }, { "key3", "value3" } };
            var keys = new[] { "key1", "key2" };

            // Act
            var result = tags.TryGetAndRemoveTag(keys, out string value);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(value, Is.EqualTo("value1"));
            Assert.That(tags, Has.Count.EqualTo(1));
            Assert.That(tags.ContainsKey("key3"), Is.True);
        }

        [Test]
        public void TryGetAndRemoveTag_Should_Return_True_For_Second_Key_And_Remove_All_Keys()
        {
            // Arrange
            var tags = new Dictionary<string, object> { { "key2", "value2" }, { "key3", "value3" } };
            var keys = new[] { "key1", "key2" };

            // Act
            var result = tags.TryGetAndRemoveTag(keys, out string value);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(value, Is.EqualTo("value2"));
            Assert.That(tags, Has.Count.EqualTo(1));
            Assert.That(tags.ContainsKey("key3"), Is.True);
        }

        [Test]
        public void TryGetAndRemoveTag_Should_Return_False_When_Key_Does_Not_Exist_And_Remove_Nothing()
        {
            // Arrange
            var tags = new Dictionary<string, object> { { "key1", "value1" } };
            var keys = new[] { "nonexistent" };

            // Act
            var result = tags.TryGetAndRemoveTag(keys, out string value);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(value, Is.Null);
            Assert.That(tags, Has.Count.EqualTo(1));
        }

        [Test]
        public void TryGetAndRemoveTag_Should_Return_False_When_Value_Is_Wrong_Type_And_Remove_Key()
        {
            // Arrange
            var tags = new Dictionary<string, object> { { "key1", 123 } };
            var keys = new[] { "key1" };

            // Act
            var result = tags.TryGetAndRemoveTag(keys, out string value);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(value, Is.Null);
            Assert.That(tags, Is.Empty);
        }

        [Test]
        public void TryGetAndRemoveTag_Should_Remove_Key_Even_If_Not_Found_For_Value_Retrieval()
        {
            // Arrange
            var tags = new Dictionary<string, object> { { "key2", "value2" } };
            var keys = new[] { "key1", "key2" }; // key1 will be removed, key2 will be found and removed

            // Act
            var result = tags.TryGetAndRemoveTag(keys, out string value);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(value, Is.EqualTo("value2"));
            Assert.That(tags, Is.Empty);
        }
    }
}
