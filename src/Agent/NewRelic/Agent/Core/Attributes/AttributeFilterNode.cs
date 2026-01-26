// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Attributes;

internal class AttributeFilterNode
{
    public readonly string Key;

    public readonly bool Wildcard;

    public readonly AttributeDestinations DestinationIncludes;
    public readonly AttributeDestinations DestinationExcludes;

    public AttributeFilterNode(string key, AttributeDestinations includes, AttributeDestinations excludes)
    {
        if (key.EndsWith("*"))
        {
            Wildcard = true;
            Key = key.Substring(0, key.Length - 1);
        }
        else
        {
            Key = key;
        }
        DestinationIncludes = includes;
        DestinationExcludes = excludes;
    }
}