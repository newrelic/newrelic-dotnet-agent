// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core.BrowserMonitoring
{
    [TestFixture]
    public class BrowserMonitoringConfigurationDataTests
    {
        [Test]
        public void SerializesCorrectly()
        {
            var javaScriptAgentLoaderData = new BrowserMonitoringConfigurationData("licenseKey", "beacon", "errorBeacon", "browserMonitoringKey", "applicationId", "obfuscatedTransactionName", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "jsAgentPayloadFile", "obfuscatedFormattedAttributes", true);

            const string expectedJson = @"{""beacon"":""beacon"",""errorBeacon"":""errorBeacon"",""licenseKey"":""browserMonitoringKey"",""applicationID"":""applicationId"",""transactionName"":""obfuscatedTransactionName"",""queueTime"":1000,""applicationTime"":2000,""agent"":""jsAgentPayloadFile"",""atts"":""obfuscatedFormattedAttributes"",""sslForHttp"":""true""}";
            Assert.That(javaScriptAgentLoaderData.ToJsonString(), Is.EqualTo(expectedJson));
        }

        [Test]
        public void SerializesCorrectly_IfMissingOptionalAttributes()
        {
            var javaScriptAgentLoaderData = new BrowserMonitoringConfigurationData("licenseKey", "beacon", "errorBeacon", "browserMonitoringKey", "applicationId", "obfuscatedTransactionName", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "jsAgentPayloadFile", null, false);

            const string expectedJson = @"{""beacon"":""beacon"",""errorBeacon"":""errorBeacon"",""licenseKey"":""browserMonitoringKey"",""applicationID"":""applicationId"",""transactionName"":""obfuscatedTransactionName"",""queueTime"":1000,""applicationTime"":2000,""agent"":""jsAgentPayloadFile"",""atts"":""""}";
            Assert.That(javaScriptAgentLoaderData.ToJsonString(), Is.EqualTo(expectedJson));
        }
    }
}
