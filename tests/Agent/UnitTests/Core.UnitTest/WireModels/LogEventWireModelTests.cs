// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Collections;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.WireModels
{
    [TestFixture]
    public class LogEventWireModelTests
    {
        private Dictionary<string, object> _testContextData = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };
        [Test]
        public void ConstructorTest()
        {
            var expectedTimestamp = 1234L;
            var expectedMessage = "Log Message FTW!";
            var expectedLevel = "TestLevel";
            var expectedSpanId = "TestSpanId";
            var expectedTraceId = "ExpectedTraceId";
            float expectedPriority = 0;

            var objectUnderTest = new LogEventWireModel(expectedTimestamp, expectedMessage, expectedLevel, expectedSpanId, expectedTraceId, _testContextData);

            Assert.That(objectUnderTest, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(objectUnderTest.TimeStamp, Is.EqualTo(expectedTimestamp));
                Assert.That(objectUnderTest.Message, Is.EqualTo(expectedMessage));
                Assert.That(objectUnderTest.Level, Is.EqualTo(expectedLevel));
                Assert.That(objectUnderTest.SpanId, Is.EqualTo(expectedSpanId));
                Assert.That(objectUnderTest.TraceId, Is.EqualTo(expectedTraceId));
                Assert.That(objectUnderTest.Priority, Is.EqualTo(expectedPriority));
                Assert.That(objectUnderTest.ContextData, Is.EqualTo(_testContextData));
            });
        }

        [Test]
        public void ImplementsIHasPriority()
        {
            var expectedPriority = 33.3f;
            var baseObject = new LogEventWireModel(0, "", "", "", "", _testContextData);
            baseObject.Priority = expectedPriority;

            var objectUnderTest = baseObject as IHasPriority;

            Assert.That(objectUnderTest, Is.Not.Null);
            Assert.That(objectUnderTest.Priority, Is.EqualTo(expectedPriority));
        }

        [Test]
        [TestCase(-66.6f)] // Less than minimum priority
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidPriorityThrowsAndRetainsPreviousValue(float priority)
        {
            var startingPriority = 33.3f;
            var objectUnderTest = new LogEventWireModel(0, "", "", "", "", _testContextData)
            {
                Priority = startingPriority
            };

            Assert.Throws<ArgumentException>(() => { objectUnderTest.Priority = priority; });
            Assert.That(objectUnderTest.Priority, Is.EqualTo(startingPriority));
        }

        [Test]
        public void MessageIsTruncatedTo32Kb()
        {
            var maxLogMessageLengthInBytes = 32 * 1024;
            var reallyLongMessageString = new string('a', maxLogMessageLengthInBytes);
            var tooLongMessageString = reallyLongMessageString + "a few too many chars";

            var logEvent = new LogEventWireModel(0, tooLongMessageString, "INFO", "", "", _testContextData);

            var messageStringFromLogEvent = logEvent.Message;

            Assert.That(maxLogMessageLengthInBytes, Is.EqualTo(Encoding.UTF8.GetByteCount(messageStringFromLogEvent)));
        }
    }
}
