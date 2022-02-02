// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Reflection;
using System.Collections.Generic;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Testing.Assertions;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Linq;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Utilities
{
    [TestFixture]
    public class LogEventWireModelCollectionJsonConverterTests
    {
        [Test]
        public void ModelIsJsonSerializable()
        {
            var sourceObject = new LogEventWireModelCollection(
                "name",
                "type",
                "guid",
                "hostname",
                "plugintype",
                new List<LogEventWireModel>()
                {
                    new LogEventWireModel(1, "message", "level", "spanId", "traceId")
                    {
                        Priority = 33.3f
                    }
                });

            var serialized = JsonConvert.SerializeObject(sourceObject, Formatting.None);
            Assert.AreEqual(
                "{\"common\":{\"attributes\":{\"entity.name\":\"name\",\"entity.type\":\"type\",\"entity.guid\":\"guid\"," +
                "\"hostname\":\"hostname\",\"plugin.type\":\"plugintype\"}},\"logs\":[{\"timestamp\":1,\"message\":\"message\"," +
                "\"level\":\"level\",\"attributes\":{\"spanid\":\"spanId\",\"traceid\":\"traceId\"}}]}",
                serialized);
        }
    }
}
