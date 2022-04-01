// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
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

            var loggingEvents = new List<LogEventWireModel>
            {
                new LogEventWireModel(1, "TestMessage", "TestLevel", "TestSpanId", "TestTraceId")
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
            Assert.AreEqual("TestLevel", loggingEvent.LogLevel);
            Assert.AreEqual("TestSpanId", loggingEvent.SpanId);
            Assert.AreEqual("TestTraceId", loggingEvent.TraceId);
        }
    }
}
