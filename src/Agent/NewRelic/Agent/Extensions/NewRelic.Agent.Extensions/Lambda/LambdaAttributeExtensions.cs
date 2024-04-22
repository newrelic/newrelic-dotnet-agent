// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;

namespace NewRelic.Agent.Extensions.Lambda;

public static class LambdaAttributeExtensions
{
    public static void AddEventSourceAttribute(this ITransaction transaction, string suffix, object value)
    {
        // This is faster than string interpolation
        transaction.AddLambdaAttribute("aws.lambda.eventSource." + suffix, value);
    }
}
