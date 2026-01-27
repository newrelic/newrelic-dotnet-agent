// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.BrowserMonitoring;

//NREUM.info={
//  "beacon":"staging-beacon-1.newrelic.com",
//  "errorBeacon":"staging-jserror.newrelic.com",
//  "licenseKey":"3ed9cebafb",
//  "applicationID":"45526",
//  "transactionName":"J1lZFRQMVF9VFk4AWwtRRE4PDVxWSA==",
//  "queueTime":0,
//  "applicationTime":116,
//  "agent":"js-agent.newrelic.com/nr-213.min.js",
//  "userAttributes":"SxNHQFFHFA0aSl9cV1JeVkoWGRRUTUpEXl9vWFYRDgEESg=="
//   "sslForHttp":"true"
//}

public class BrowserMonitoringConfigurationData
{
    private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };

    [JsonProperty("beacon")]
    public string Beacon { get; }

    [JsonProperty("errorBeacon")]
    public string ErrorBeacon { get; }

    [JsonProperty("licenseKey")]
    public string BrowserLicenseKey { get; }

    [JsonProperty("applicationID")]
    public string ApplicationId { get; }

    [JsonProperty("transactionName")]
    public string ObfuscatedTransactionName { get; }

    [JsonProperty("queueTime")]
    public int QueueTimeMilliseconds => (int)_queueTime.TotalMilliseconds;
    private readonly TimeSpan _queueTime;

    [JsonProperty("applicationTime")]
    public int ApplicationTimeMilliseconds => (int)_applicationTime.TotalMilliseconds;
    private readonly TimeSpan _applicationTime;

    [JsonProperty("agent")]
    public string Agent { get; }

    [JsonProperty("atts")]
    public string ObfuscatedUserAttributes { get; }

    [JsonProperty("sslForHttp", NullValueHandling = NullValueHandling.Ignore)]
    public string SslForHttp => _sslForHttp ? "true" : null;
    private readonly bool _sslForHttp;

    public BrowserMonitoringConfigurationData(string licenseKey, string beacon, string errorBeacon, string browserMonitoringKey, string applicationId, string obfuscatedTransactionName, TimeSpan queueTime, TimeSpan applicationTime, string jsAgentPayloadFile, string obfuscatedFormattedAttributes, bool sslForHttp)
    {
        Beacon = beacon;
        ErrorBeacon = errorBeacon;
        BrowserLicenseKey = browserMonitoringKey;
        ApplicationId = applicationId;
        ObfuscatedTransactionName = obfuscatedTransactionName;
        _queueTime = queueTime;
        _applicationTime = applicationTime;
        Agent = jsAgentPayloadFile;
        ObfuscatedUserAttributes = obfuscatedFormattedAttributes ?? string.Empty;
        _sslForHttp = sslForHttp;
    }

    public string ToJsonString()
    {

        return JsonConvert.SerializeObject(this, Formatting.None, _jsonSettings);
    }

}
