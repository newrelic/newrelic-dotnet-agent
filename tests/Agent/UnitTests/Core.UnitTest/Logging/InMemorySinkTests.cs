// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using Serilog.Events;
using System;
using System.Linq;
using Serilog.Parsing;

namespace NewRelic.Agent.Core.Logging.Tests
{
    [TestFixture]
    public class InMemorySinkTests
    {
        private InMemorySink _sink;
        private LogEvent _logEvent;

        [SetUp]
        public void SetUp()
        {
            _sink = new InMemorySink();
            _logEvent = new LogEvent(DateTimeOffset.Now, LogEventLevel.Information, null, new MessageTemplate("Test", Enumerable.Empty<MessageTemplateToken>()), Enumerable.Empty<LogEventProperty>());
        }

        [TearDown]
        public void TearDown()
        {
            _sink.Dispose();
        }
        [Test]
        public void Emit_Enqueues_LogEvent()
        {
            _sink.Emit(_logEvent);
            Assert.That(_sink.LogEvents.Count(), Is.EqualTo(1));
        }

        [Test]
        public void LogEvents_ReturnsCorrectEvents()
        {
            _sink.Emit(_logEvent);
            var logEvents = _sink.LogEvents;

            Assert.Multiple(() =>
            {
                Assert.That(logEvents.Count(), Is.EqualTo(1));
                Assert.That(logEvents.First(), Is.EqualTo(_logEvent));
            });
        }

        [Test]
        public void Clear_LogEvents_EmptiesQueue()
        {
            _sink.Emit(_logEvent);
            _sink.Clear();
            Assert.That(_sink.LogEvents.Count(), Is.EqualTo(0));
        }

        [Test]
        public void Dispose_ClearsLogEvents()
        {
            _sink.Emit(_logEvent);
            _sink.Dispose();
            Assert.That(_sink.LogEvents.Count(), Is.EqualTo(0));
        }
    }
}
