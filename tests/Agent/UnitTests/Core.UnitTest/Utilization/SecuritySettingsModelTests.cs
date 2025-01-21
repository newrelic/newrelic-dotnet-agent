// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Newtonsoft.Json;
using NewRelic.Agent.Core.DataTransport;
using NUnit.Framework;

namespace NewRelic.Agent.Core.DataTransport.Tests
{
    [TestFixture]
    public class SecuritySettingsModelTests
    {
        [Test]
        public void TestJsonSerialization()
        {
            var transactionTraceSettings = new TransactionTraceSettingsModel("obfuscated");
            var model = new SecuritySettingsModel(transactionTraceSettings);
            var json = JsonConvert.SerializeObject(model);

            Assert.Multiple(() =>
            {
                Assert.That(json, Is.Not.Null);
                Assert.That(json, Does.Contain("\"transaction_tracer\":{\"record_sql\":\"obfuscated\"}"));
            });
        }

        [Test]
        public void TestJsonSerializationWithNullValues()
        {
            var transactionTraceSettings = new TransactionTraceSettingsModel(null);
            var model = new SecuritySettingsModel(transactionTraceSettings);
            var json = JsonConvert.SerializeObject(model);

            Assert.Multiple(() =>
            {
                Assert.That(json, Is.Not.Null);
                Assert.That(json, Does.Contain("\"transaction_tracer\":{\"record_sql\":null}"));
            });
        }
    }
}

