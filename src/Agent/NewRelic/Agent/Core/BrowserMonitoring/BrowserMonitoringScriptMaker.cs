// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Core;
using NewRelic.Core.Logging;
using Newtonsoft.Json;
using System;
using System.Web;

namespace NewRelic.Agent.Core.BrowserMonitoring
{
    public interface IBrowserMonitoringScriptMaker
    {
        string GetScript(IInternalTransaction transaction, string nonce);
    }

    public class BrowserMonitoringScriptMaker : IBrowserMonitoringScriptMaker
    {
        private readonly BrowserEventWireModelSerializer _jsonConverter = new BrowserEventWireModelSerializer();

        private readonly IConfigurationService _configurationService;

        private readonly ITransactionMetricNameMaker _transactionMetricNameMaker;

        private readonly ITransactionAttributeMaker _transactionAttributeMaker;

        private readonly IAttributeDefinitionService _attribDefSvc;

        private static readonly TimeSpan _zeroTimespan = TimeSpan.FromSeconds(0);

        public BrowserMonitoringScriptMaker(IConfigurationService configurationService, ITransactionMetricNameMaker transactionMetricNameMaker, ITransactionAttributeMaker transactionAttributeMaker, IAttributeDefinitionService attribDefSvc)
        {
            _configurationService = configurationService;
            _transactionMetricNameMaker = transactionMetricNameMaker;
            _transactionAttributeMaker = transactionAttributeMaker;
            _attribDefSvc = attribDefSvc;
        }

        public string GetScript(IInternalTransaction transaction, string nonce)
        {
            if (string.IsNullOrEmpty(_configurationService.Configuration.BrowserMonitoringJavaScriptAgent))
            {
                Log.Debug("Skipping RUM injection because \"js_agent_loader\" config is missing or empty");
                return null;
            }

            if (_configurationService.Configuration.BrowserMonitoringJavaScriptAgentLoaderType.Equals("none",
                    StringComparison.InvariantCultureIgnoreCase))
            {
                Log.Debug("Skipping RUM injection because \"browser_monitoring.loader\" config is \"none\"");
                return null;
            }

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
            // serverless mode doesn't require a license key, so this check can fail.
            if (string.IsNullOrEmpty(licenseKey) || "REPLACE_WITH_LICENSE_KEY".Equals(licenseKey))
            {
                Log.Debug("Skipping RUM injection because license key is not set.");
                return null;
            }

            var browserConfigurationData = GetBrowserConfigurationData(transaction, transactionMetricName, licenseKey);

            // getting a stack trace is expensive, so only do it if we are going to log
            if (Log.IsFinestEnabled)
                transaction.LogFinest("RUM: TryGetBrowserTimingHeader success.");

            var scriptNonce = String.IsNullOrEmpty(nonce) ? String.Empty : $@" nonce=""{HttpUtility.HtmlAttributeEncode(nonce)}""";

            // The JavaScript variable NREUMQ stands for New Relic End User Metric "Q". This was the name before marketing renamed "EUM" to "RUM".
            // We can't change the name of the variable since: (a) we have to be consistent across agents, and (b) it has to be in sync with the rum.js file which is downloaded from NR servers.
            var javascriptAgentConfiguration = $"window.NREUM||(NREUM={{}});NREUM.info = {browserConfigurationData.ToJsonString()}";
            var javascriptAgent = _configurationService.Configuration.BrowserMonitoringJavaScriptAgent;
            return $"<script type=\"text/javascript\"{scriptNonce}>{javascriptAgentConfiguration}</script><script type=\"text/javascript\"{scriptNonce}>{javascriptAgent}</script>";
        }

        private BrowserMonitoringConfigurationData GetBrowserConfigurationData(IInternalTransaction transaction, TransactionMetricName transactionMetricName, string licenseKey)
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

            var attributes = new AttributeValueCollection(AttributeDestinations.JavaScriptAgent);
            _transactionAttributeMaker.SetUserAndAgentAttributes(attributes, transaction.TransactionMetadata);

            // for now, treat tripId as an agent attribute when passing to browser.  Eventually this will be an intrinsic but need changes to browser code first.
            // if CrossApplicationReferrerTripId is null then this transaction started the first external request, so use its guid
            _attribDefSvc.AttributeDefs.BrowserTripId.TrySetValue(attributes, transaction.TransactionMetadata.CrossApplicationReferrerTripId ?? transaction.Guid);

            //This will resolve any Lazy values and remove null values from the collection.
            //Browser monitoring should not render JSON for attributes if there are none
            attributes.MakeImmutable();

            var obfuscatedFormattedAttributes = GetObfuscatedFormattedAttributes(attributes, licenseKey);
            var sslForHttp = configuration.BrowserMonitoringUseSsl;

            return new BrowserMonitoringConfigurationData(licenseKey, beacon, errorBeacon, browserMonitoringKey, applicationId, obfuscatedTransactionName, queueTime, applicationTime, jsAgentPayloadFile, obfuscatedFormattedAttributes, sslForHttp);
        }

        private string GetObfuscatedFormattedAttributes(AttributeValueCollection attribValues, string licenseKey)
        {
            if (attribValues == null)
            {
                return null;
            }

            var json = JsonConvert.SerializeObject(attribValues, _jsonConverter);

            return string.IsNullOrWhiteSpace(json)
                ? null
                : Strings.ObfuscateStringWithKey(json, licenseKey);
        }
    }
}
