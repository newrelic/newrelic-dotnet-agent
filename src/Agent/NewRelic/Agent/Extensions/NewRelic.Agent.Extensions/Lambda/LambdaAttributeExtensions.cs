// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Api;

namespace NewRelic.Agent.Extensions.Lambda;

public static class LambdaAttributeExtensions
{
    public static void AddEventSourceAttribute(this Dictionary<string, string> dict, string suffix, string value)
    {
        dict.Add($"aws.lambda.eventSource.{suffix}", value);
    }

    public static void AddLambdaAttributes(this ITransaction transaction, Dictionary<string, string> attributes)
    {
        foreach (var attribute in attributes)
        {
            transaction.AddCustomAttribute(attribute.Key, attribute.Value); // TODO: figure out if custom attributes are correct
        }
    }
}
