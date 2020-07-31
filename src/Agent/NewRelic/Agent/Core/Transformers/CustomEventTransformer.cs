// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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
        void Transform(string eventType, IEnumerable<KeyValuePair<string, object>> attributes);
    }

    public class CustomEventTransformer : ICustomEventTransformer
    {
        private const int EventTypeValueLengthLimit = 256;
        private const int CustomAttributeLengthLimit = 256;
        private const string EventTypeRegexText = @"^[a-zA-Z0-9:_ ]{1,256}$";
        private static readonly Regex EventTypeRegex = new Regex(EventTypeRegexText, RegexOptions.Compiled);
        private readonly IConfigurationService _configurationService;
        private readonly ICustomEventAggregator _customEventAggregator;

        public CustomEventTransformer(IConfigurationService configurationService, ICustomEventAggregator customEventAggregator)
        {
            _configurationService = configurationService;
            _customEventAggregator = customEventAggregator;
        }

        public void Transform(string eventType, IEnumerable<KeyValuePair<string, object>> attributes)
        {
            if (!_configurationService.Configuration.CustomEventsEnabled)
                return;

            if (eventType.Length > EventTypeValueLengthLimit)
                throw new Exception($"CustomEvent dropped because eventType string was larger than {EventTypeValueLengthLimit} characters.");
            if (!EventTypeRegex.Match(eventType).Success)
                throw new Exception($"CustomEvent dropped because eventType string did not conform to the following regex: {EventTypeRegexText}");

            attributes = attributes as IList<KeyValuePair<string, object>> ?? attributes.ToList();

            LogNullValuedAttributes(attributes);

            var filteredUserAttributes = attributes
                .Where(attribute => attribute.Value is string || attribute.Value is float)
                .Where(attribute => attribute.Value?.ToString().Length < CustomAttributeLengthLimit);

            var customEvent = new CustomEventWireModel(eventType, DateTime.UtcNow, filteredUserAttributes);
            _customEventAggregator.Collect(customEvent);
        }

        private static void LogNullValuedAttributes(IEnumerable<KeyValuePair<string, object>> attributes)
        {
            var nullValuedAttributeNames = attributes.Where(attribute => attribute.Value == null).Select(attribute => attribute.Key).ToArray();
            if (!nullValuedAttributeNames.Any())
                return;

            Log.Debug($"CUSTOM EVENT: The following attributes had null values and will be ignored: {string.Join(",", nullValuedAttributeNames)}");
        }
    }
}
