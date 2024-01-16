// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

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

            ClassicAssert.NotNull(objectUnderTest);
            ClassicAssert.AreEqual(entityGuid, objectUnderTest.EntityGuid);
            ClassicAssert.AreEqual(entityName, objectUnderTest.EntityName);
            ClassicAssert.AreEqual(hostname, objectUnderTest.Hostname);
            ClassicAssert.AreEqual(1, objectUnderTest.LoggingEvents.Count);

            var loggingEvent = objectUnderTest.LoggingEvents[0];
            ClassicAssert.AreEqual(1, loggingEvent.TimeStamp);
            ClassicAssert.AreEqual("TestMessage", loggingEvent.Message);
            ClassicAssert.AreEqual("TestLevel", loggingEvent.Level);
            ClassicAssert.AreEqual("TestSpanId", loggingEvent.SpanId);
            ClassicAssert.AreEqual("TestTraceId", loggingEvent.TraceId);
            ClassicAssert.AreEqual(testContextData, loggingEvent.ContextData);
        }
    }
}
