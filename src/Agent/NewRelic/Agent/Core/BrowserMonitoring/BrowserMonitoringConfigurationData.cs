using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Requests;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.BrowserMonitoring
{
	/*
	NREUM.info={
	  "beacon":"staging-beacon-1.newrelic.com",
	  "errorBeacon":"staging-jserror.newrelic.com",
	  "licenseKey":"3ed9cebafb",
	  "applicationID":"45526",
	  "transactionName":"J1lZFRQMVF9VFk4AWwtRRE4PDVxWSA==",
	  "queueTime":0,
	  "applicationTime":116,
	  "agent":"js-agent.newrelic.com/nr-213.min.js",
	  "userAttributes":"SxNHQFFHFA0aSl9cV1JeVkoWGRRUTUpEXl9vWFYRDgEESg=="
	   "sslForHttp":"true"
	}
	*/

	public class BrowserMonitoringConfigurationData
	{
		[JsonProperty("beacon")]
		[NotNull]
		public String Beacon { get; }

		[JsonProperty("errorBeacon")]
		[NotNull]
		public String ErrorBeacon { get; }

		[JsonProperty("licenseKey")]
		[NotNull]
		public String BrowserLicenseKey { get; }

		[JsonProperty("applicationID")]
		[NotNull]
		public String ApplicationId { get; }

		[JsonProperty("transactionName")]
		[NotNull]
		public String ObfuscatedTransactionName { get; }

		[JsonProperty("queueTime")]
		public Int32 QueueTimeMilliseconds => (Int32)_queueTime.TotalMilliseconds;
		private readonly TimeSpan _queueTime;

		[JsonProperty("applicationTime")]
		public Int32 ApplicationTimeMilliseconds => (Int32)_applicationTime.TotalMilliseconds;
		private readonly TimeSpan _applicationTime;

		[JsonProperty("agent")]
		[NotNull]
		public String Agent { get; }

		[JsonProperty("atts")]
		[CanBeNull]
		public String ObfuscatedUserAttributes { get; }

		[JsonProperty("sslForHttp", NullValueHandling = NullValueHandling.Ignore)]
		public String SslForHttp => _sslForHttp ? "true" : null;
		private readonly Boolean _sslForHttp;

		public BrowserMonitoringConfigurationData([NotNull] String licenseKey, [NotNull] String beacon, [NotNull] String errorBeacon, [NotNull] String browserMonitoringKey, [NotNull] String applicationId, [NotNull] String obfuscatedTransactionName, TimeSpan queueTime, TimeSpan applicationTime, [NotNull] String jsAgentPayloadFile, [CanBeNull] String obfuscatedFormattedAttributes, Boolean sslForHttp)
		{
			Beacon = beacon;
			ErrorBeacon = errorBeacon;
			BrowserLicenseKey = browserMonitoringKey;
			ApplicationId = applicationId;
			ObfuscatedTransactionName = obfuscatedTransactionName;
			_queueTime = queueTime;
			_applicationTime = applicationTime;
			Agent = jsAgentPayloadFile;
			ObfuscatedUserAttributes = obfuscatedFormattedAttributes ?? String.Empty;
			_sslForHttp = sslForHttp;
		}

		public String ToJsonString()
		{
			return JsonConvert.SerializeObject(this, Formatting.None, new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore});
		}

	}
}
