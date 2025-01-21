// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DataTransport.Tests
{
    [TestFixture]
    public class EventHarvestConfigModelTests
    {
        [Test]
        public void TestJsonSerialization()
        {
            var configuration = Mock.Create<IConfiguration>();

            Mock.Arrange(() => configuration.TransactionEventsMaximumSamplesStored).Returns(1000);
            Mock.Arrange(() => configuration.CustomEventsMaximumSamplesStored).Returns(2000);
            Mock.Arrange(() => configuration.ErrorCollectorMaxEventSamplesStored).Returns(3000);
            Mock.Arrange(() => configuration.SpanEventsMaxSamplesStored).Returns(4000);
            Mock.Arrange(() => configuration.LogEventsMaxSamplesStored).Returns(5000);

            var model = new EventHarvestConfigModel(configuration);
            var json = JsonConvert.SerializeObject(model);

            Assert.Multiple(() =>
            {
                Assert.That(json, Is.Not.Null);
                Assert.That(json, Does.Contain("\"analytic_event_data\":1000"));
                Assert.That(json, Does.Contain("\"custom_event_data\":2000"));
                Assert.That(json, Does.Contain("\"error_event_data\":3000"));
                Assert.That(json, Does.Contain("\"span_event_data\":4000"));
                Assert.That(json, Does.Contain("\"log_event_data\":5000"));
            });
        }

        [Test]
        public void TestJsonSerializationWithNullValues()
        {
            var configuration = Mock.Create<IConfiguration>();

            var model = new EventHarvestConfigModel(configuration);
            var json = JsonConvert.SerializeObject(model);

            Assert.Multiple(() =>
            {
                Assert.That(json, Is.Not.Null);
                Assert.That(json, Does.Contain("\"analytic_event_data\":0"));
                Assert.That(json, Does.Contain("\"custom_event_data\":0"));
                Assert.That(json, Does.Contain("\"error_event_data\":0"));
                Assert.That(json, Does.Contain("\"span_event_data\":0"));
                Assert.That(json, Does.Contain("\"log_event_data\":0"));
            });
        }
    }
}
