// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.DataTransport.Tests
{
    [TestFixture]
    public class TransactionTraceSettingsModelTests
    {
        [Test]
        public void TestJsonSerialization()
        {
            var model = new TransactionTraceSettingsModel("obfuscated");
            var json = JsonConvert.SerializeObject(model);

            Assert.Multiple(() =>
            {
                Assert.That(json, Is.Not.Null);
                Assert.That(json, Does.Contain("\"record_sql\":\"obfuscated\""));
            });
        }

        [Test]
        public void TestJsonSerializationWithNullValues()
        {
            var model = new TransactionTraceSettingsModel(null);
            var json = JsonConvert.SerializeObject(model);

            Assert.Multiple(() =>
            {
                Assert.That(json, Is.Not.Null);
                Assert.That(json, Does.Contain("\"record_sql\":null"));
            });
        }
    }
}
