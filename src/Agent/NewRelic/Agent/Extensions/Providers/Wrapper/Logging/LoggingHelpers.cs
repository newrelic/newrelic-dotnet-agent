// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.Logging
{
    public static class LoggingHelpers
    {
        public static string GetFormattedLinkingMetadata(IAgent agent)
        {
            var metadata = agent.GetLinkingMetadata();
            var entries = new string[metadata.Count]; // keeps the array small and light
            for (int i = 0; i < metadata.Count; i++)
            {
                var pair = metadata.ElementAt(i);
                entries[i] = pair.Key + "=" + pair.Value; // faster than string.format or interpolation
            }

            return "NR-LINKING-METADATA: {" + string.Join(", ", entries) + "}";
        }
    }
}
