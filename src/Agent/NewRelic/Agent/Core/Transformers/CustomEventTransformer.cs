using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transformers
{
    public interface ICustomEventTransformer
    {
        void Transform(String eventType, IEnumerable<KeyValuePair<String, Object>> attributes);
    }

    public class CustomEventTransformer : ICustomEventTransformer
    {
        private const Int32 EventTypeValueLengthLimit = 256;
        private const Int32 CustomAttributeLengthLimit = 256;
        private const String EventTypeRegexText = @"^[a-zA-Z0-9:_ ]{1,256}$";
        private static readonly Regex EventTypeRegex = new Regex(EventTypeRegexText, RegexOptions.Compiled);
        private readonly IConfigurationService _configurationService;
        private readonly ICustomEventAggregator _customEventAggregator;

        public CustomEventTransformer(IConfigurationService configurationService, ICustomEventAggregator customEventAggregator)
        {
            _configurationService = configurationService;
            _customEventAggregator = customEventAggregator;
        }

        public void Transform(String eventType, IEnumerable<KeyValuePair<String, Object>> attributes)
        {
            if (!_configurationService.Configuration.CustomEventsEnabled)
                return;

            if (eventType.Length > EventTypeValueLengthLimit)
                throw new Exception($"CustomEvent dropped because eventType string was larger than {EventTypeValueLengthLimit} characters.");
            if (!EventTypeRegex.Match(eventType).Success)
                throw new Exception($"CustomEvent dropped because eventType string did not conform to the following regex: {EventTypeRegexText}");

            attributes = attributes as IList<KeyValuePair<String, Object>> ?? attributes.ToList();

            LogNullValuedAttributes(attributes);

            var filteredUserAttributes = attributes
                .Where(attribute => attribute.Value is String || attribute.Value is Single)
                .Where(attribute => attribute.Value?.ToString().Length < CustomAttributeLengthLimit);

            var customEvent = new CustomEventWireModel(eventType, DateTime.UtcNow, filteredUserAttributes);
            _customEventAggregator.Collect(customEvent);
        }

        private static void LogNullValuedAttributes(IEnumerable<KeyValuePair<String, Object>> attributes)
        {
            var nullValuedAttributeNames = attributes.Where(attribute => attribute.Value == null).Select(attribute => attribute.Key).ToArray();
            if (!nullValuedAttributeNames.Any())
                return;

            Log.Debug($"CUSTOM EVENT: The following attributes had null values and will be ignored: {String.Join(",", nullValuedAttributeNames)}");
        }
    }
}
