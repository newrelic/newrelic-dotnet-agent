// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using System.Web;

namespace NewRelic.Agent.Extensions.Logging
{
    public static class LoggingHelpers
    {
        private const string EntityName = "entity.name";
        private const string EntityGuid = "entity.guid";
        private const string Hostname = "hostname";
        private const string TraceId = "trace.id";
        private const string SpanId = "span.id";

        public static string GetFormattedLinkingMetadata(IAgent agent)
        {
            var metadata = agent.GetLinkingMetadata();

            string entityName = string.Empty;
            if (metadata.ContainsKey(EntityName))
            {
                entityName = HttpUtility.UrlEncode(metadata[EntityName]);
            }

            string entityGuid = string.Empty;
            if (metadata.ContainsKey(EntityGuid))
            {
                entityGuid = metadata[EntityGuid];
            }

            string hostname = string.Empty;
            if (metadata.ContainsKey(Hostname))
            {
                hostname = metadata[Hostname];
            }

            string traceId = string.Empty;
            if (metadata.ContainsKey(TraceId))
            {
                traceId = metadata[TraceId];
            }

            string spanId = string.Empty;
            if (metadata.ContainsKey(SpanId))
            {
                spanId = metadata[SpanId];
            }

            // This is a positional blob so we want the delimiters left in when no data is  present.
            // NR-LINKING|{entity.guid}|{hostname}|{trace.id}|{span.id}|
            return "NR-LINKING|" + entityGuid + "|" + hostname + "|" + traceId + "|" + spanId + "|" + entityName + "|";
        }
    }
}
