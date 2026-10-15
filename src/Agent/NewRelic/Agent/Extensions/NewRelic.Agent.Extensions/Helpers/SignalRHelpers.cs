// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Extensions.Helpers;

public static class SignalRHelpers
{
    private const string HubMetadataTypeName = "Microsoft.AspNetCore.SignalR.HubMetadata";
    private const string NegotiateMetadataTypeName = "Microsoft.AspNetCore.Http.Connections.NegotiateMetadata";

    /// <summary>
    /// Returns true for the request shape SignalR dispatches as a Server-Sent Events transport:
    /// a GET with an event-stream Accept header and a connection id query value.
    /// </summary>
    public static bool IsServerSentEventsTransportRequest(string method, string acceptHeader, string connectionIdQueryValue)
    {
        return string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            && acceptHeader != null
            && acceptHeader.IndexOf("text/event-stream", StringComparison.OrdinalIgnoreCase) >= 0
            && !string.IsNullOrEmpty(connectionIdQueryValue);
    }

    /// <summary>
    /// Returns true when the endpoint metadata belongs to a SignalR hub transport endpoint
    /// (long polling, server-sent events, or websockets): hub metadata without negotiate metadata.
    /// Types are matched by full name so no SignalR assembly reference is needed.
    /// </summary>
    public static bool IsHubTransportEndpoint(IEnumerable<object> endpointMetadata)
    {
        if (endpointMetadata == null)
        {
            return false;
        }

        var hasHubMetadata = false;
        foreach (var item in endpointMetadata)
        {
            var typeName = item?.GetType().FullName;
            if (typeName == NegotiateMetadataTypeName)
            {
                return false;
            }

            if (typeName == HubMetadataTypeName)
            {
                hasHubMetadata = true;
            }
        }

        return hasHubMetadata;
    }
}
