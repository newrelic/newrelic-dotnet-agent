// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.WireModels
{
    [TestFixture]
    public class LogEventWireModelCollectionTests
    {
        [Test]
        public void ConstructorTest()
        {
            var entityName = "MyApplicationName";
            var entityGuid = Guid.NewGuid().ToString();
            var hostname = "TestHostname";

            var testContextData = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };

            var loggingEvents = new List<LogEventWireModel>
            {
                new LogEventWireModel(1, "TestMessage", "TestLevel", "TestSpanId", "TestTraceId", testContextData)
            };

            var objectUnderTest = new LogEventWireModelCollection(entityName, entityGuid, hostname, loggingEvents);

            Assert.NotNull(objectUnderTest);
            Assert.AreEqual(entityGuid, objectUnderTest.EntityGuid);
            Assert.AreEqual(entityName, objectUnderTest.EntityName);
            Assert.AreEqual(hostname, objectUnderTest.Hostname);
            Assert.AreEqual(1, objectUnderTest.LoggingEvents.Count);

            var loggingEvent = objectUnderTest.LoggingEvents[0];
            Assert.AreEqual(1, loggingEvent.TimeStamp);
            Assert.AreEqual("TestMessage", loggingEvent.Message);
            Assert.AreEqual("TestLevel", loggingEvent.Level);
            Assert.AreEqual("TestSpanId", loggingEvent.SpanId);
            Assert.AreEqual("TestTraceId", loggingEvent.TraceId);
            Assert.AreEqual(testContextData, loggingEvent.ContextData);
        }

        [Test]
        public void LogEventWireModelCollection_SerializesCorrectly()
        {
            // Arrange
            const string expected = @"{""common"":{""attributes"":{""entity.name"":""MyApplicationName"",""entity.guid"":""a4edc699-4c47-4599-90b5-3c4c55512e96"",""hostname"":""TestHostname""}},""logs"":[{""timestamp"":1,""message"":""TestMessage1"",""level"":""TestLevel"",""span.id"":""TestSpanId1"",""trace.id"":""TestTraceId1"",""attributes"":{""context.key1"":""value1"",""context.key2"":1}},{""timestamp"":1,""message"":""TestMessage2"",""level"":""TestLevel"",""span.id"":""TestSpanId2"",""trace.id"":""TestTraceId2""},{""timestamp"":1,""message"":""TestMessage3"",""level"":""TestLevel"",""span.id"":""TestSpanId3"",""trace.id"":""TestTraceId3""}]}";

            var entityName = "MyApplicationName";
            var entityGuid = "a4edc699-4c47-4599-90b5-3c4c55512e96";
            var hostname = "TestHostname";

            var testContextData = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };

            var loggingEvents = new List<LogEventWireModel>
            {
                new LogEventWireModel(1, "TestMessage1", "TestLevel", "TestSpanId1", "TestTraceId1", testContextData),
                new LogEventWireModel(1, "TestMessage2", "TestLevel", "TestSpanId2", "TestTraceId2", null),
                new LogEventWireModel(1, "TestMessage3", "TestLevel", "TestSpanId3", "TestTraceId3", new Dictionary<string, object>())
            };

            var objectUnderTest = new LogEventWireModelCollection(entityName, entityGuid, hostname, loggingEvents);

            // Act
            var actual = JsonConvert.SerializeObject(objectUnderTest);

            // Assert
            Assert.AreEqual(expected, actual);
        }
    }
}
