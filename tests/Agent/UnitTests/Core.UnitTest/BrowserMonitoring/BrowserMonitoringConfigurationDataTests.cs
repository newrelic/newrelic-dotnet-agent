// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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
            ClassicAssert.AreEqual(expectedJson, javaScriptAgentLoaderData.ToJsonString());
        }

        [Test]
        public void SerializesCorrectly_IfMissingOptionalAttributes()
        {
            var javaScriptAgentLoaderData = new BrowserMonitoringConfigurationData("licenseKey", "beacon", "errorBeacon", "browserMonitoringKey", "applicationId", "obfuscatedTransactionName", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "jsAgentPayloadFile", null, false);

            const string expectedJson = @"{""beacon"":""beacon"",""errorBeacon"":""errorBeacon"",""licenseKey"":""browserMonitoringKey"",""applicationID"":""applicationId"",""transactionName"":""obfuscatedTransactionName"",""queueTime"":1000,""applicationTime"":2000,""agent"":""jsAgentPayloadFile"",""atts"":""""}";
            ClassicAssert.AreEqual(expectedJson, javaScriptAgentLoaderData.ToJsonString());
        }
    }
}
