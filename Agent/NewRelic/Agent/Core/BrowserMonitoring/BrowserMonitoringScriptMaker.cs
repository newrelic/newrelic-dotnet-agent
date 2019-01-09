using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Utils;
using Newtonsoft.Json;
using Attribute = NewRelic.Agent.Core.Transactions.Attribute;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.BrowserMonitoring
{
	public interface IBrowserMonitoringScriptMaker
	{
		string GetScript(ITransaction transaction);
	}

	public class BrowserMonitoringScriptMaker : IBrowserMonitoringScriptMaker
	{
		private readonly IConfigurationService _configurationService;

		private readonly ITransactionMetricNameMaker _transactionMetricNameMaker;

		private readonly ITransactionAttributeMaker _transactionAttributeMaker;

		private readonly IAttributeService _attributeService;

		private static readonly TimeSpan _zeroTimespan = TimeSpan.FromSeconds(0);

		public BrowserMonitoringScriptMaker(IConfigurationService configurationService, ITransactionMetricNameMaker transactionMetricNameMaker, ITransactionAttributeMaker transactionAttributeMaker, IAttributeService attributeService)
		{
			_configurationService = configurationService;
			_transactionMetricNameMaker = transactionMetricNameMaker;
			_transactionAttributeMaker = transactionAttributeMaker;
			_attributeService = attributeService;
		}

		public string GetScript(ITransaction transaction)
		{
			if (string.IsNullOrEmpty(_configurationService.Configuration.BrowserMonitoringJavaScriptAgent))
				return null;

			if (_configurationService.Configuration.BrowserMonitoringJavaScriptAgentLoaderType.Equals("none", StringComparison.InvariantCultureIgnoreCase))
				return null;

			if (transaction.Ignored)
			{
				Log.Debug("Skipping RUM injection because transaction is ignored");
				return null;
			}

			var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(transaction.CandidateTransactionName.CurrentTransactionName);
			if (transactionMetricName.ShouldIgnore)
			{
				Log.Debug("Skipping RUM injection because transaction name is ignored");
				return null;
			}

			var licenseKey = _configurationService.Configuration.AgentLicenseKey;
			if (licenseKey == null)
				throw new NullReferenceException(nameof(licenseKey));
			if (licenseKey.Length <= 0)
				throw new Exception("License key is empty");

			var browserConfigurationData = GetBrowserConfigurationData(transaction, transactionMetricName, licenseKey);

			// getting a stack trace is expensive, so only do it if we are going to log
			if (Log.IsFinestEnabled)
				Log.FinestFormat("RUM: TryGetBrowserTimingHeader success at {0}", new System.Diagnostics.StackTrace(true));

			// The JavaScript variable NREUMQ stands for New Relic End User Metric "Q". This was the name before marketing renamed "EUM" to "RUM".
			// We can't change the name of the variable since: (a) we have to be consistent across agents, and (b) it has to be in sync with the rum.js file which is downloaded from NR servers.
			var javascriptAgentConfiguration = $"window.NREUM||(NREUM={{}});NREUM.info = {browserConfigurationData.ToJsonString()}";
			var javascriptAgent = _configurationService.Configuration.BrowserMonitoringJavaScriptAgent;
			return $"<script type=\"text/javascript\">{javascriptAgentConfiguration}</script><script type=\"text/javascript\">{javascriptAgent}</script>";
		}
		
		private BrowserMonitoringConfigurationData GetBrowserConfigurationData(ITransaction transaction, TransactionMetricName transactionMetricName, string licenseKey)
		{
			var configuration = _configurationService.Configuration;

			var beacon = configuration.BrowserMonitoringBeaconAddress;
			if (beacon == null)
				throw new NullReferenceException(nameof(beacon));

			var errorBeacon = configuration.BrowserMonitoringErrorBeaconAddress;
			if (errorBeacon == null)
				throw new NullReferenceException(nameof(errorBeacon));

			var browserMonitoringKey = configuration.BrowserMonitoringKey;
			if (browserMonitoringKey == null)
				throw new NullReferenceException(nameof(browserMonitoringKey));

			var applicationId = configuration.BrowserMonitoringApplicationId;
			if (applicationId == null)
				throw new NullReferenceException(nameof(applicationId));

			var jsAgentPayloadFile = configuration.BrowserMonitoringJavaScriptAgentFile;
			if (jsAgentPayloadFile == null)
				throw new NullReferenceException(nameof(jsAgentPayloadFile));

			var obfuscatedTransactionName = Strings.ObfuscateStringWithKey(transactionMetricName.PrefixedName, licenseKey);
			var queueTime = transaction.TransactionMetadata.QueueTime ?? _zeroTimespan;
			var applicationTime = transaction.GetDurationUntilNow();
			var attributes = _transactionAttributeMaker.GetUserAndAgentAttributes(transaction.TransactionMetadata);

			// for now, treat tripId as an agent attribute when passing to browser.  Eventually this will be an intrinsic but need changes to browser code first.
			// if CrossApplicationReferrerTripId is null then this transaction started the first external request, so use its guid
			var tripId = transaction.TransactionMetadata.CrossApplicationReferrerTripId ?? transaction.Guid;
			attributes.TryAddAll(Attribute.BuildBrowserTripIdAttribute, tripId);

			var obfuscatedFormattedAttributes = GetObfuscatedFormattedAttributes(attributes, licenseKey);
			var sslForHttp = configuration.BrowserMonitoringUseSsl;

			return new BrowserMonitoringConfigurationData(licenseKey, beacon, errorBeacon, browserMonitoringKey, applicationId, obfuscatedTransactionName, queueTime, applicationTime, jsAgentPayloadFile, obfuscatedFormattedAttributes, sslForHttp);
		}

		private string GetObfuscatedFormattedAttributes(Attributes attributes,string licenseKey)
		{
			if (attributes == null || attributes.Count() == 0)
			{
				return null;
			}

			var filteredAttributes = _attributeService.FilterAttributes(attributes, AttributeDestinations.JavaScriptAgent);

			var agentAttributes = filteredAttributes.GetAgentAttributesDictionary();

			var userAttributes = filteredAttributes.GetUserAttributesDictionary();

			if (agentAttributes.IsEmpty() && userAttributes.IsEmpty())
			{
				return null;
			}

			var model = new BrowserMonitoringWireModel(agentAttributes, userAttributes);

			var formattedAttributes = JsonConvert.SerializeObject(model, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
			 
			return formattedAttributes != null ? Strings.ObfuscateStringWithKey(formattedAttributes, licenseKey) : null;
		}
	}
}
