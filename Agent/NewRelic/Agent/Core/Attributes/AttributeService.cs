using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Memoization;

namespace NewRelic.Agent.Core.Attributes
{
	public interface IAttributeService
	{
		AttributeCollection FilterAttributes(AttributeCollection attributes, AttributeDestinations attributeDestination);

		bool AllowRequestUri(AttributeDestinations attributeDestination);
	}

	public class AttributeService : ConfigurationBasedService, IAttributeService
	{
		public const string AgentAttributesKey = "agentAttributes";
		public const string UserAttributesKey = "userAttributes";
		public const string IntrinsicsKey = "intrinsics";

		private IAttributeFilter _attributeFilter { get { return Memoizer.Memoize(ref _attributeFilterBacker, CreateAttributeFilter); } }
		private IAttributeFilter _attributeFilterBacker;

		private IAttributeFilter _intrinsicsAttributeFilter = CreateIntrinsicsAttributeFilter();

		#region Event Handlers

		public AttributeCollection FilterAttributes(AttributeCollection attributes, AttributeDestinations attributeDestination)
		{
			var filteredAttributes = new AttributeCollection();

			var filteredAgentAttrs = attributes.GetAgentAttributes()
				.FilterAttributes(_attributeFilter, attributeDestination);

			foreach (var agentAttr in filteredAgentAttrs)
			{
				filteredAttributes.Add(agentAttr);
			}

			var filteredUserAttrs = attributes.GetUserAttributes()
				.Take(AttributeCollection.UserAttributeClamp)
				.FilterAttributes(_attributeFilter, attributeDestination, true)
				.ToArray();

			foreach (var userAttr in filteredUserAttrs)
			{
				filteredAttributes.Add(userAttr);
			}

			var filteredIntrinsicAttrs = attributes.GetIntrinsics()
				.FilterAttributes(_intrinsicsAttributeFilter, attributeDestination);

			foreach (var intrinsic in filteredIntrinsicAttrs)
			{
				filteredAttributes.Add(intrinsic);
			}

			return filteredAttributes;
		}

		public bool AllowRequestUri(AttributeDestinations attributeDestination)
		{
			var attributes = new List<Attribute> { Attribute.BuildRequestUriAttribute(string.Empty) };

			var filteredAttrs = attributes.FilterAttributes(_attributeFilter, attributeDestination);

			return filteredAttrs.Any();
		}

		#endregion

		#region Private Helpers

		private IAttributeFilter CreateAttributeFilter()
		{
			var attributeFilterSettings = new AttributeFilter.Settings
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

				TransactionEventEnabled = _configuration.TransactionEventsAttributesEnabled,
				TransactionEventIncludes = _configuration.TransactionEventsAttributesInclude,
				TransactionEventExcludes = _configuration.TransactionEventsAttributesExclude,

				TransactionTraceEnabled = _configuration.CaptureTransactionTraceAttributes,
				TransactionTraceIncludes = _configuration.CaptureTransactionTraceAttributesIncludes,
				TransactionTraceExcludes = _configuration.CaptureTransactionTraceAttributesExcludes,

				ErrorEventsEnabled = _configuration.ErrorCollectorCaptureEvents,
				ErrorEventIncludes = _configuration.CaptureErrorCollectorAttributesIncludes,
				ErrorEventExcludes = _configuration.CaptureErrorCollectorAttributesExcludes,

				SpanEventsEnabled = _configuration.SpanEventsEnabled,
				SpanEventIncludes = _configuration.SpanEventsAttributesInclude,
				SpanEventExcludes = _configuration.SpanEventsAttributesExclude,

				CustomEventsEnabled = _configuration.CustomEventsEnabled,
				CustomEventIncludes = _configuration.CustomEventsAttributesInclude,
				CustomEventExcludes = _configuration.CustomEventsAttributesExclude
			};
			return new AttributeFilter(attributeFilterSettings);
		}

		private static IAttributeFilter CreateIntrinsicsAttributeFilter()
		{
			var attributeFilterSettings = new AttributeFilter.Settings();
			return new AttributeFilter(attributeFilterSettings);
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
		public static IEnumerable<Attribute> FilterAttributes(this IEnumerable<Attribute> attributes, IAttributeFilter attributeFilter, AttributeDestinations destination)
		{
			return FilterAttributes(attributes, attributeFilter, destination, false);
		}

		public static IEnumerable<Attribute> FilterAttributes(this IEnumerable<Attribute> attributes, IAttributeFilter attributeFilter, AttributeDestinations destination, bool logInvalidAttribs)
		{
			if (attributes == null)
				return Enumerable.Empty<Attribute>();

			return attributeFilter.FilterAttributes(attributes, destination, logInvalidAttribs);
		}
	}
}
