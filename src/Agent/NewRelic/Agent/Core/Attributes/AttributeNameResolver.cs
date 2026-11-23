// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.Attributes;

/// <summary>
/// Computes the full name and the allowed destinations of a dynamic attribute from the attribute filter.
/// </summary>
public static class AttributeNameResolver
{
    private static readonly AttributeDestinations[] PrefixedDestinations =
    {
        AttributeDestinations.TransactionEvent,
        AttributeDestinations.TransactionTrace,
        AttributeDestinations.ErrorTrace,
        AttributeDestinations.ErrorEvent,
        AttributeDestinations.SpanEvent
    };

    public static ResolvedAttributeName ResolveCustom(IAttributeFilter filter, string rawName, AttributeDestinations familyDestinations)
    {
        var availability = new Dictionary<AttributeDestinations, bool>();

        AttributeAvailability.Set(availability, familyDestinations, true);
        AttributeAvailability.ApplyFilter(availability, rawName, filter);

        return new ResolvedAttributeName(rawName, AttributeClassification.UserAttributes, AttributeAvailability.ToFlags(availability));
    }

    public static ResolvedAttributeName ResolvePrefixed(IAttributeFilter filter, string prefix, string rawName)
    {
        var fullName = prefix + rawName;
        var availability = new Dictionary<AttributeDestinations, bool>();

        foreach (var destination in PrefixedDestinations)
        {
            AttributeAvailability.Set(availability, destination, filter.CheckOrAddAttributeClusionCache(fullName, AttributeDestinations.None, destination));
        }

        AttributeAvailability.ApplyFilter(availability, fullName, filter);

        return new ResolvedAttributeName(fullName, AttributeClassification.AgentAttributes, AttributeAvailability.ToFlags(availability));
    }
}
