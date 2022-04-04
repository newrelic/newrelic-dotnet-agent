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
            var sourceObject = new LogEventWireModelCollection(
                "myApplicationName",
                "guid",
                "hostname",
                new List<LogEventWireModel>()
                {
                    new LogEventWireModel(1, "message", "log.level", "spanId", "traceId")
                    {
                        Priority = 33.3f
                    }
                });

            var serialized = JsonConvert.SerializeObject(sourceObject, Formatting.None);

            Assert.AreEqual(
                "{\"common\":{\"attributes\":{\"entity.name\":\"myApplicationName\",\"entity.guid\":\"guid\",\"hostname\":\"hostname\"}}," +
                "\"logs\":[{\"timestamp\":1,\"message\":\"message\",\"log.level\":\"log.level\"," +
                "\"attributes\":{\"span.id\":\"spanId\",\"trace.id\":\"traceId\"}}]}",
                serialized);
        }
    }
}
