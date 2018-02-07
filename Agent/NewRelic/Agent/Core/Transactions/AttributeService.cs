using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using MoreLinq;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Requests;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Memoization;

namespace NewRelic.Agent.Core.Transactions
{
	public interface IAttributeService
	{
		[NotNull]
		Attributes FilterAttributes([NotNull] Attributes attributes, AttributeDestinations attributeDestination);
	}

	public class AttributeService : ConfigurationBasedService, IAttributeService
	{
		private const int CUSTOM_ATTRIBUTE_CLAMP = 64;

		public const string AgentAttributesKey = "agentAttributes";
		public const string UserAttributesKey = "userAttributes";
		public const string IntrinsicsKey = "intrinsics";

		[NotNull]
		private IAttributeFilter<Attribute> _attributeFilter { get { return Memoizer.Memoize(ref _attributeFilterBacker, CreateAttributeFilter); } }
		private IAttributeFilter<Attribute> _attributeFilterBacker;

		#region Event Handlers

		public Attributes FilterAttributes([NotNull] Attributes attributes, AttributeDestinations attributeDestination)
		{
			var filteredAttributes = new Attributes();

			attributes.GetAgentAttributes()
				.Where(ShouldIncludeInHighSecurity)
				.FilterAttributes(_attributeFilter, attributeDestination)
				.ForEach(filteredAttributes.Add);

			attributes.GetUserAttributes()
				.Where(_ => _configuration.CaptureCustomParameters)
				.Take(CUSTOM_ATTRIBUTE_CLAMP)
				.Where(ShouldIncludeInHighSecurity)
				.FilterAttributes(_attributeFilter, attributeDestination)
				.ForEach(filteredAttributes.Add);

			attributes.GetIntrinsics()
				.Where(ShouldIncludeInHighSecurity)
				.FilterAttributes(_attributeFilter, attributeDestination)
				.ForEach(filteredAttributes.Add);

			return filteredAttributes;
		}


		#endregion

		#region Private Helpers

		private Boolean ShouldIncludeInHighSecurity(Attribute attribute)
		{
			if (!_configuration.HighSecurityModeEnabled)
				return true;

			if (attribute.ExcludeForHighSecurity)
				return false;
			else
				return true;
		}

		private IAttributeFilter<Attribute> CreateAttributeFilter()
		{
			var attributeFilterSettings = new AttributeFilter<Attribute>.Settings
			{
				AttributesEnabled = _configuration.CaptureAttributes,
				Includes = _configuration.CaptureAttributesIncludes,
				Excludes = _configuration.CaptureAttributesExcludes,

				ErrorTraceEnabled = _configuration.CaptureErrorCollectorAttributes,
				ErrorTraceIncludes = _configuration.CaptureErrorCollectorAttributesIncludes,
				ErrorTraceExcludes = _configuration.CaptureErrorCollectorAttributesExcludes,

				JavaScriptAgentEnabled = _configuration.CaptureBrowserMonitoringAttributes,
				JavaScriptAgentIncludes = _configuration.CaptureBrowserMonitoringAttributesIncludes,
				JavaScriptAgentExcludes = _configuration.CaptureBrowserMonitoringAttributesExcludes,

				TransactionEventEnabled = _configuration.CaptureTransactionEventsAttributes,
				TransactionEventIncludes = _configuration.CaptureTransactionEventAttributesIncludes,
				TransactionEventExcludes = _configuration.CaptureTransactionEventAttributesExcludes,

				TransactionTraceEnabled = _configuration.CaptureTransactionTraceAttributes,
				TransactionTraceIncludes = _configuration.CaptureTransactionTraceAttributesIncludes,
				TransactionTraceExcludes = _configuration.CaptureTransactionTraceAttributesExcludes,

				ErrorEventsEnabled = _configuration.ErrorCollectorCaptureEvents,
				ErrorEventIncludes = _configuration.CaptureErrorCollectorAttributesIncludes,
				ErrorEventExcludes = _configuration.CaptureErrorCollectorAttributesExcludes
			};
			return new AttributeFilter<Attribute>(attributeFilterSettings);
		}

		#endregion

		#region Service Base

		protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
		{
			// It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
			// If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).

			_attributeFilterBacker = null;
		}

		#endregion
	}

	internal static class AttributeFilterExtensions
	{
		[NotNull]
		public static IEnumerable<Attribute> FilterAttributes(this IEnumerable<Attribute> attributes, [NotNull] IAttributeFilter<Attribute> attributeFilter, AttributeDestinations destination)
		{
			if (attributes == null)
				return Enumerable.Empty<Attribute>();

			return attributeFilter.FilterAttributes(attributes, destination);
		}
	}
}