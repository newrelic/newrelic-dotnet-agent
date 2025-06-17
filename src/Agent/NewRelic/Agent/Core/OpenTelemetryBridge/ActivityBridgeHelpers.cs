// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Diagnostics;
using NewRelic.Agent.Api;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    public static class ActivityBridgeHelpers
    {
        private static readonly List<int> _activityKindsThatStartATransaction =
        [
            (int)ActivityKind.Server,
            (int)ActivityKind.Consumer
        ];

        public static bool IsTransactionRequiredForActivity(object originalActivity)
        {
            // TODO: Determine if this is the right thing to do. Our wrapper service separates these concepts.
            return !ShouldStartTransactionForActivity(originalActivity);
        }

        public static bool ShouldStartTransactionForActivity(object originalActivity)
        {
            dynamic activity = originalActivity;

            return (bool)activity.HasRemoteParent || _activityKindsThatStartATransaction.Contains((int)activity.Kind);
        }

        public static ITransaction StartTransactionForActivity(object originalActivity, IAgent agent)
        {
            dynamic activity = originalActivity;

            bool isWeb = (int)activity.Kind == (int)ActivityKind.Server;

            return agent.CreateTransaction(isWeb, "Activity", activity.DisplayName, doNotTrackAsUnitOfWork: true);
        }

        public static IEnumerable<string> GetTraceContextHeadersFromActivity(object originalActivity, string headerName)
        {
            dynamic activity = originalActivity;
            switch (headerName)
            {
                case "traceparent":
                    return [(string)activity.ParentId];
                case "tracestate":
                    return [(string)activity.TraceStateString ?? string.Empty];
                default:
                    return [];
            }
        }

    }
}
