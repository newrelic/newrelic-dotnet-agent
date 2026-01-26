// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text.RegularExpressions;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.SystemExtensions;

namespace NewRelic.Agent.Core.Transformers;

public interface ICustomEventTransformer
{
    void Transform(string eventType, IEnumerable<KeyValuePair<string, object>> attributes, float priority);
}

public class CustomEventTransformer : ICustomEventTransformer
{
    private const string EventTypeRegexText = @"^[a-zA-Z0-9:_ ]{1,256}$";
    private const int CustomEventTypeMaxLengthBytes = 255;

    private static readonly Regex EventTypeRegex = new Regex(EventTypeRegexText, RegexOptions.Compiled);

    private readonly IConfigurationService _configurationService;

    private readonly ICustomEventAggregator _customEventAggregator;
    private readonly IAttributeDefinitionService _attribDefSvc;
    private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;

    public CustomEventTransformer(IConfigurationService configurationService, ICustomEventAggregator customEventAggregator, IAttributeDefinitionService attribDefSvc)
    {
        _configurationService = configurationService;
        _customEventAggregator = customEventAggregator;
        _attribDefSvc = attribDefSvc;
    }

    public void Transform(string eventType, IEnumerable<KeyValuePair<string, object>> attributes, float priority)
    {
        if (!_configurationService.Configuration.CustomEventsEnabled)
        {
            return;
        }

        eventType = eventType?.Trim();
        var eventTypeSize = eventType.SizeBytes();

        if (eventTypeSize > CustomEventTypeMaxLengthBytes)
        {
            Log.Debug($"Custom Event could not be added - Event Type was larger than {CustomEventTypeMaxLengthBytes} bytes.");
            return;
        }

        if (eventTypeSize == 0)
        {
            Log.Debug($"Custom Event could not be added - Event Type was null/empty");
            return;
        }

        if (!EventTypeRegex.Match(eventType).Success)
        {
            Log.Debug($"Custom Event could not be added - Event Type did not conform to the following regex: {EventTypeRegexText}");
            return;
        }

        var attribValues = new AttributeValueCollection(AttributeDestinations.CustomEvent);

        _attribDefs.CustomEventType.TrySetValue(attribValues, eventType);
        _attribDefs.Timestamp.TrySetDefault(attribValues);

        if (_configurationService.Configuration.CustomEventsAttributesEnabled)
        {
            foreach(var customAttrib in attributes)
            {
                _attribDefs.GetCustomAttributeForCustomEvent(customAttrib.Key).TrySetValue(attribValues, customAttrib.Value);
            }
        }

        var customEvent = new CustomEventWireModel(priority, attribValues);
        _customEventAggregator.Collect(customEvent);
    }
}
