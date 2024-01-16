// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Serilog.Events;
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
            ClassicAssert.AreEqual(1, _sink.LogEvents.Count());
        }

        [Test]
        public void LogEvents_ReturnsCorrectEvents()
        {
            _sink.Emit(_logEvent);
            var logEvents = _sink.LogEvents;

            ClassicAssert.AreEqual(1, logEvents.Count());
            ClassicAssert.AreEqual(_logEvent, logEvents.First());
        }

        [Test]
        public void Clear_LogEvents_EmptiesQueue()
        {
            _sink.Emit(_logEvent);
            _sink.Clear();
            ClassicAssert.AreEqual(0, _sink.LogEvents.Count());
        }

        [Test]
        public void Dispose_ClearsLogEvents()
        {
            _sink.Emit(_logEvent);
            _sink.Dispose();
            ClassicAssert.AreEqual(0, _sink.LogEvents.Count());
        }
    }
}
