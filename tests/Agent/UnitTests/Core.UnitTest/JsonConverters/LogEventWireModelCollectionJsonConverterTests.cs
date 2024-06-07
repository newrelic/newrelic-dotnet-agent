// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;
using NUnit.Framework;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Utilities
{
    [TestFixture]
    public class LogEventWireModelCollectionJsonConverterTests
    {
        [Test]
        public void LogEventWireModelCollectionIsJsonSerializable()
        {
            var expected = @"{""common"":{""attributes"":{""entity.name"":""myApplicationName"",""entity.guid"":""guid"",""hostname"":""hostname""}},""logs"":[{""timestamp"":1,""message"":""TestMessage1"",""level"":""TestLevel"",""span.id"":""TestSpanId1"",""trace.id"":""TestTraceId1"",""attributes"":{""context.key1"":""value1"",""context.key2"":1,""context.key3"":{""Foo"":1,""Bar"":2}}},{""timestamp"":1,""message"":""TestMessage2"",""level"":""TestLevel"",""span.id"":""TestSpanId2"",""trace.id"":""TestTraceId2""},{""timestamp"":1,""message"":""TestMessage3"",""level"":""TestLevel"",""span.id"":""TestSpanId3"",""trace.id"":""TestTraceId3""},{""timestamp"":1,""message"":""TestMessage4"",""level"":""TestLevel"",""error.stack"":""foo \nbar"",""error.message"":""errorMessage"",""error.class"":""errorClass"",""span.id"":""TestSpanId4"",""trace.id"":""TestTracedId4"",""attributes"":{""context.key1"":""value1"",""context.key2"":1,""context.key3"":{""Foo"":1,""Bar"":2}}}]}";

            var _contextData = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 }, {"key3", new { Foo = 1, Bar = 2 } } };
            var sourceObject = new LogEventWireModelCollection(
            "myApplicationName",
            "guid",
            "hostname",
            new List<LogEventWireModel>()
            {
                new LogEventWireModel(1, "TestMessage1", "TestLevel", "TestSpanId1", "TestTraceId1", _contextData),
                new LogEventWireModel(1, "TestMessage2", "TestLevel", "TestSpanId2", "TestTraceId2", null),
                new LogEventWireModel(1, "TestMessage3", "TestLevel", "TestSpanId3", "TestTraceId3", new Dictionary<string, object>()),
                new LogEventWireModel(1, "TestMessage4", "TestLevel", new string[] {"foo", "bar" }, "errorMessage", "errorClass", "TestSpanId4", "TestTracedId4", _contextData)
            });

            var serialized = JsonConvert.SerializeObject(sourceObject, Formatting.None);
            Assert.That(serialized, Is.EqualTo(expected));
        }

        [Test]
        public void DeserializeObjectFailsWithNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() =>JsonConvert.DeserializeObject<LogEventWireModelCollection>("{}"));
        }
    }
}
