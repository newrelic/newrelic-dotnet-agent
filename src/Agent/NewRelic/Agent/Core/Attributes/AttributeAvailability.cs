// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.Attributes;

internal static class AttributeAvailability
{
    private static readonly AttributeDestinations[] AttribDestinationValues =
        Enum.GetValues(typeof(AttributeDestinations)).OfType<AttributeDestinations>()
            .Where(x => x != AttributeDestinations.None)
            .Where(x => x != AttributeDestinations.All)
            .ToArray();

    public static void Set(Dictionary<AttributeDestinations, bool> availability, AttributeDestinations destination, bool isAvailable)
    {
        foreach (var val in AttribDestinationValues)
        {
            if ((destination & val) == val)
            {
                availability[val] = isAvailable;
            }
        }
    }

    public static void ApplyFilter(Dictionary<AttributeDestinations, bool> availability, string name, IAttributeFilter filter)
    {
        foreach (var dest in availability.Keys.ToArray())
        {
            availability[dest] = availability[dest]
                                 && !filter.ShouldFilterAttribute(dest)
                                 && filter.CheckOrAddAttributeClusionCache(name, dest, dest);
        }
    }

    public static AttributeDestinations ToFlags(Dictionary<AttributeDestinations, bool> availability)
    {
        var destinations = AttributeDestinations.None;

        foreach (var entry in availability)
        {
            if (entry.Value)
            {
                destinations |= entry.Key;
            }
        }

        return destinations;
    }
}
