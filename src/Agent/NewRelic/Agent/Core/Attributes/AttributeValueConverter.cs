// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Attributes;

/// <summary>
/// The convert and post-process steps shared by the attribute definitions.
/// </summary>
public static class AttributeValueConverter
{
    /// <returns>false when the input, the converted value, or the post-processed value is null, or when convert is null</returns>
    public static bool TryConvert<TInput, TOutput>(TInput input, Func<TInput, TOutput> convert, Func<TOutput, TOutput> postProcess, out TOutput output)
    {
        output = default;

        if (input == null || convert == null)
        {
            return false;
        }

        var converted = convert(input);
        if (converted == null)
        {
            return false;
        }

        if (postProcess != null)
        {
            converted = postProcess(converted);
            if (converted == null)
            {
                return false;
            }
        }

        output = converted;
        return true;
    }
}
