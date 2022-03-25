// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
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
        [Test]
        public void ConstructorTest()
        {
            var expectedTimestamp = 1234L;
            var expectedMessage = "Log Message FTW!";
            var expectedLevel = "TestLogLevel";
            var expectedSpanId = "TestSpanId";
            var expectedTraceId = "ExpectedTraceId";
            float expectedPriority = 0;

            var objectUnderTest = new LogEventWireModel(expectedTimestamp, expectedMessage, expectedLevel, expectedSpanId, expectedTraceId);

            Assert.NotNull(objectUnderTest);
            Assert.AreEqual(expectedTimestamp, objectUnderTest.TimeStamp);
            Assert.AreEqual(expectedMessage, objectUnderTest.Message);
            Assert.AreEqual(expectedLevel, objectUnderTest.Level);
            Assert.AreEqual(expectedSpanId, objectUnderTest.SpanId);
            Assert.AreEqual(expectedTraceId, objectUnderTest.TraceId);
            Assert.AreEqual(expectedPriority, objectUnderTest.Priority);
        }

        [Test]
        public void ImplementsIHasPriority()
        {
            var expectedPriority = 33.3f;
            var baseObject = new LogEventWireModel(0, "", "", "", "");
            baseObject.Priority = expectedPriority;

            var objectUnderTest = baseObject as IHasPriority;

            Assert.NotNull(objectUnderTest);
            Assert.AreEqual(expectedPriority, objectUnderTest.Priority);
        }

        [Test]
        [TestCase(-66.6f)] // Less than minimum priority
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidPriorityThrowsAndRetainsPreviousValue(float priority)
        {
            var startingPriority = 33.3f;
            var objectUnderTest = new LogEventWireModel(0, "", "", "", "")
            {
                Priority = startingPriority
            };

            Assert.Throws<ArgumentException>(() => { objectUnderTest.Priority = priority; });
            Assert.AreEqual(startingPriority, objectUnderTest.Priority);
        }

        [Test]
        public void MessageIsTruncatedTo32Kb()
        {
            var maxLogMessageLengthInBytes = 32 * 1024;
            var reallyLongMessageString = new string('a', maxLogMessageLengthInBytes);
            var tooLongMessageString = reallyLongMessageString + "a few too many chars";

            var logEvent = new LogEventWireModel(0, tooLongMessageString, "INFO", "", "");

            var messageStringFromLogEvent = logEvent.Message;

            Assert.AreEqual(Encoding.UTF8.GetByteCount(messageStringFromLogEvent), maxLogMessageLengthInBytes);
        }
    }
}
