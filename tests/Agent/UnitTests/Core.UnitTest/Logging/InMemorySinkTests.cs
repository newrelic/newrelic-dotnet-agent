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

        [Test]
        public void Emit_Enqueues_LogEvent()
        {
            _sink.Emit(_logEvent);
            Assert.AreEqual(1, _sink.LogEvents.Count());
        }

        [Test]
        public void LogEvents_ReturnsCorrectEvents()
        {
            _sink.Emit(_logEvent);
            var logEvents = _sink.LogEvents;

            Assert.AreEqual(1, logEvents.Count());
            Assert.AreEqual(_logEvent, logEvents.First());
        }

        [Test]
        public void Clear_LogEvents_EmptiesQueue()
        {
            _sink.Emit(_logEvent);
            _sink.Clear();
            Assert.AreEqual(0, _sink.LogEvents.Count());
        }

        [Test]
        public void Dispose_ClearsLogEvents()
        {
            _sink.Emit(_logEvent);
            _sink.Dispose();
            Assert.AreEqual(0, _sink.LogEvents.Count());
        }
    }
}
