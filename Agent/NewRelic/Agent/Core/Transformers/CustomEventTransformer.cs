using System.Collections.Generic;
using System.Text.RegularExpressions;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.Transformers
{
	public interface ICustomEventTransformer
	{
		void Transform(string eventType, IEnumerable<KeyValuePair<string,object>> attributes, float priority);
	}

	public class CustomEventTransformer : ICustomEventTransformer
	{
		private const string EventTypeRegexText = @"^[a-zA-Z0-9:_ ]{1,256}$";

		private static readonly Regex EventTypeRegex = new Regex(EventTypeRegexText, RegexOptions.Compiled);

		private readonly IConfigurationService _configurationService;

		private readonly ICustomEventAggregator _customEventAggregator;

		private readonly IAttributeService _attribSvc;

		public CustomEventTransformer(IConfigurationService configurationService, IAttributeService attribSvc,
			ICustomEventAggregator customEventAggregator)
		{
			_configurationService = configurationService;
			_customEventAggregator = customEventAggregator;
			_attribSvc = attribSvc;
		}

		public void Transform(string eventType, IEnumerable<KeyValuePair<string,object>> attributes, float priority)
		{
			if (!_configurationService.Configuration.CustomEventsEnabled)
			{
				return;
			}

			var eventTypeAttrib = Attributes.Attribute.BuildCustomEventTypeAttribute(eventType.Trim());

			if (eventTypeAttrib.IsValueTruncated)
			{
				Log.Debug($"Custom Event could not be added - Event Type was larger than {Attributes.Attribute.CUSTOM_ATTRIBUTE_VALUE_LENGTH_CLAMP} bytes.");
				return;
			}

			if (string.IsNullOrWhiteSpace(eventTypeAttrib.Value))
			{
				Log.Debug($"Custom Event could not be added - Event Type was null/empty");
				return;
			}

			if (!EventTypeRegex.Match(eventTypeAttrib.Value).Success)
			{
				Log.Debug($"Custom Event could not be added - Event Type did not conform to the following regex: {EventTypeRegexText}");
				return;
			}

			var attribCollection = new AttributeCollection();

			attribCollection.Add(eventTypeAttrib);
			attribCollection.Add(Attribute.BuildTimestampAttribute(System.DateTime.UtcNow));

			if (_configurationService.Configuration.CustomEventsAttributesEnabled)
			{
				attribCollection.TryAddAll(Attribute.BuildCustomAttributeForCustomEvent, attributes);
			}

			//Filter, Validate, and Log problems with attributes.
			attribCollection  = _attribSvc.FilterAttributes(attribCollection, AttributeDestinations.CustomEvent);
			
			var customEvent = new CustomEventWireModel(priority, attribCollection.GetIntrinsicsDictionary(), attribCollection.GetUserAttributesDictionary());
			_customEventAggregator.Collect(customEvent);
		}
	}
}
