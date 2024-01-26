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

            Assert.That(objectUnderTest, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(objectUnderTest.EntityGuid, Is.EqualTo(entityGuid));
                Assert.That(objectUnderTest.EntityName, Is.EqualTo(entityName));
                Assert.That(objectUnderTest.Hostname, Is.EqualTo(hostname));
                Assert.That(objectUnderTest.LoggingEvents, Has.Count.EqualTo(1));
            });

            var loggingEvent = objectUnderTest.LoggingEvents[0];
            Assert.Multiple(() =>
            {
                Assert.That(loggingEvent.TimeStamp, Is.EqualTo(1));
                Assert.That(loggingEvent.Message, Is.EqualTo("TestMessage"));
                Assert.That(loggingEvent.Level, Is.EqualTo("TestLevel"));
                Assert.That(loggingEvent.SpanId, Is.EqualTo("TestSpanId"));
                Assert.That(loggingEvent.TraceId, Is.EqualTo("TestTraceId"));
                Assert.That(loggingEvent.ContextData, Is.EqualTo(testContextData));
            });
        }
    }
}
