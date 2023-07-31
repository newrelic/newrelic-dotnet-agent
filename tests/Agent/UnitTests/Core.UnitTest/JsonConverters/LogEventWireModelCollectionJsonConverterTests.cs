// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
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
            var expected = @"{""common"":{""attributes"":{""entity.name"":""myApplicationName"",""entity.guid"":""guid"",""hostname"":""hostname""}},""logs"":[{""timestamp"":1,""message"":""TestMessage1"",""level"":""TestLevel"",""span.id"":""TestSpanId1"",""trace.id"":""TestTraceId1"",""attributes"":{""context.key1"":""value1"",""context.key2"":1}},{""timestamp"":1,""message"":""TestMessage2"",""level"":""TestLevel"",""span.id"":""TestSpanId2"",""trace.id"":""TestTraceId2""},{""timestamp"":1,""message"":""TestMessage3"",""level"":""TestLevel"",""span.id"":""TestSpanId3"",""trace.id"":""TestTraceId3""}]}";

            var _contextData = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };
            var sourceObject = new LogEventWireModelCollection(
            "myApplicationName",
            "guid",
            "hostname",
            new List<LogEventWireModel>()
            {
                new LogEventWireModel(1, "TestMessage1", "TestLevel", "TestSpanId1", "TestTraceId1", _contextData),
                new LogEventWireModel(1, "TestMessage2", "TestLevel", "TestSpanId2", "TestTraceId2", null),
                new LogEventWireModel(1, "TestMessage3", "TestLevel", "TestSpanId3", "TestTraceId3", new Dictionary<string, object>())
            });

            var serialized = JsonConvert.SerializeObject(sourceObject, Formatting.None);
            Assert.AreEqual(expected, serialized);
        }
    }
}
