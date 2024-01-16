// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Core.Attributes.Tests.Models
{
    public class Configuration
    {
        [JsonProperty(PropertyName = "attributes.enabled")]
        public bool AttributesEnabled = true;

        [JsonProperty(PropertyName = "browser_monitoring.attributes.enabled")]
        public bool BrowserMonitoringAttributesEnabled = false;

        [JsonProperty(PropertyName = "error_collector.attributes.enabled")]
        public bool ErrorCollectorAttributesEnabled = true;

        [JsonProperty(PropertyName = "transaction_events.attributes.enabled")]
        public bool TransactionEventsAttributesEnabled = true;

        [JsonProperty(PropertyName = "transaction_tracer.attributes.enabled")]
        public bool TransactionTracerAttributesEnabled = true;

        [JsonProperty(PropertyName = "attributes.include")]
        public IEnumerable<string> AttributesInclude = Enumerable.Empty<string>();

        [JsonProperty(PropertyName = "attributes.exclude")]
        public IEnumerable<string> AttributesExclude = Enumerable.Empty<string>();

        [JsonProperty(PropertyName = "browser_monitoring.attributes.exclude")]
        public IEnumerable<string> BrowserMonitoringAttributeExcludes = Enumerable.Empty<string>();

        [JsonProperty(PropertyName = "browser_monitoring.attributes.include")]
        public IEnumerable<string> BrowserMonitoringAttributeIncludes = Enumerable.Empty<string>();

        [JsonProperty(PropertyName = "error_collector.attributes.exclude")]
        public IEnumerable<string> ErrorCollectorAttributeExcludes = Enumerable.Empty<string>();

        [JsonProperty(PropertyName = "error_collector.attributes.include")]
        public IEnumerable<string> ErrorCollectorAttributeIncludes = Enumerable.Empty<string>();

        [JsonProperty(PropertyName = "transaction_events.attributes.exclude")]
        public IEnumerable<string> TransactionEventsAttributeExcludes = Enumerable.Empty<string>();

        [JsonProperty(PropertyName = "transaction_events.attributes.include")]
        public IEnumerable<string> TransactionEventsAttributeIncludes = Enumerable.Empty<string>();

        [JsonProperty(PropertyName = "span_events.attributes.exclude")]
        public IEnumerable<string> SpanEventAttributeExcludes = Enumerable.Empty<string>();

        [JsonProperty(PropertyName = "span_events.attributes.include")]
        public IEnumerable<string> SpanEventAttributeIncludes = Enumerable.Empty<string>();

        [JsonProperty(PropertyName = "transaction_tracer.attributes.exclude")]
        public IEnumerable<string> TransactionTracerAttributeExcludes = Enumerable.Empty<string>();

        [JsonProperty(PropertyName = "transaction_tracer.attributes.include")]
        public IEnumerable<string> TransactionTracerAttributeIncludes = Enumerable.Empty<string>();

        public AttributeFilter.Settings ToAttributeFilterSettings()
        {
            return new AttributeFilter.Settings
            {
                AttributesEnabled = AttributesEnabled,
                JavaScriptAgentEnabled = BrowserMonitoringAttributesEnabled,
                ErrorTraceEnabled = ErrorCollectorAttributesEnabled,
                TransactionEventEnabled = TransactionEventsAttributesEnabled,
                TransactionTraceEnabled = TransactionTracerAttributesEnabled,

                Includes = AttributesInclude,
                Excludes = AttributesExclude,

                JavaScriptAgentIncludes = BrowserMonitoringAttributeIncludes,
                JavaScriptAgentExcludes = BrowserMonitoringAttributeExcludes,

                ErrorTraceIncludes = ErrorCollectorAttributeIncludes,
                ErrorTraceExcludes = ErrorCollectorAttributeExcludes,

                TransactionEventIncludes = TransactionEventsAttributeIncludes,
                TransactionEventExcludes = TransactionEventsAttributeExcludes,

                TransactionTraceIncludes = TransactionTracerAttributeIncludes,
                TransactionTraceExcludes = TransactionTracerAttributeExcludes,

                SpanEventIncludes = SpanEventAttributeIncludes,
                SpanEventExcludes = SpanEventAttributeExcludes
            };
        }
    }
}
