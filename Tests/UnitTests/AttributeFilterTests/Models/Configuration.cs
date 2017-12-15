using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent;
using Newtonsoft.Json;

namespace AttributeFilterTests.Models
{
	public class Configuration
	{
		[JsonProperty(PropertyName = "attributes.enabled")]
		public Boolean AttributesEnabled = true;

		[JsonProperty(PropertyName = "browser_monitoring.attributes.enabled")]
		public Boolean BrowserMonitoringAttributesEnabled = false;

		[JsonProperty(PropertyName = "error_collector.attributes.enabled")]
		public Boolean ErrorCollectorAttributesEnabled = true;

		[JsonProperty(PropertyName = "transaction_events.attributes.enabled")]
		public Boolean TransactionEventsAttributesEnabled = true;

		[JsonProperty(PropertyName = "transaction_tracer.attributes.enabled")]
		public Boolean TransactionTracerAttributesEnabled = true;

		[JsonProperty(PropertyName = "attributes.include")]
		public IEnumerable<String> AttributesInclude = Enumerable.Empty<String>();

		[JsonProperty(PropertyName = "attributes.exclude")]
		public IEnumerable<String> AttributesExclude = Enumerable.Empty<String>();

		[JsonProperty(PropertyName = "browser_monitoring.attributes.exclude")]
		[NotNull]
		public IEnumerable<String> BrowserMonitoringAttributeExcludes = Enumerable.Empty<String>();

		[JsonProperty(PropertyName = "browser_monitoring.attributes.include")]
		[NotNull]
		public IEnumerable<String> BrowserMonitoringAttributeIncludes = Enumerable.Empty<String>();

		[JsonProperty(PropertyName = "error_collector.attributes.exclude")]
		[NotNull]
		public IEnumerable<String> ErrorCollectorAttributeExcludes = Enumerable.Empty<String>();

		[JsonProperty(PropertyName = "error_collector.attributes.include")]
		[NotNull]
		public IEnumerable<String> ErrorCollectorAttributeIncludes = Enumerable.Empty<String>();

		[JsonProperty(PropertyName = "transaction_events.attributes.exclude")]
		[NotNull]
		public IEnumerable<String> TransactionEventsAttributeExcludes = Enumerable.Empty<String>();

		[JsonProperty(PropertyName = "transaction_events.attributes.include")]
		[NotNull]
		public IEnumerable<String> TransactionEventsAttributeIncludes = Enumerable.Empty<String>();

		[JsonProperty(PropertyName = "transaction_tracer.attributes.exclude")]
		[NotNull]
		public IEnumerable<String> TransactionTracerAttributeExcludes = Enumerable.Empty<String>();

		[JsonProperty(PropertyName = "transaction_tracer.attributes.include")]
		[NotNull]
		public IEnumerable<String> TransactionTracerAttributeIncludes = Enumerable.Empty<String>();

		[NotNull]
		public AttributeFilter<Attribute>.Settings ToAttributeFilterSettings()
		{
			return new AttributeFilter<Attribute>.Settings
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
			};
		}
	}
}
