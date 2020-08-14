// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Api.Experimental
{
    /// <summary>
    /// Helper extension methods for accessing the experimental APIs
    /// </summary>
    public static class ExperimentalApiExtensionMethods
    {
        public static ITransactionExperimental GetExperimentalApi(this ITransaction transaction)
        {
            return transaction as ITransactionExperimental;
        }

        public static IAgentExperimental GetExperimentalApi(this IAgent agent)
        {
            return agent as IAgentExperimental;
        }

        public static ISegmentExperimental GetExperimentalApi(this ISegment segment)
        {
            return segment as ISegmentExperimental;
        }
    }
}
