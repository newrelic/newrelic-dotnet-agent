// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Extensions.Parsing;

public static class SqlMetadataCommentBuilder
{
    public static readonly HashSet<string> ValidKeys = new(System.StringComparer.Ordinal)
    {
        "nr_service",
        "nr_service_guid",
        "nr_txn",
        "nr_trace_id"
    };

    public static string BuildComment(IReadOnlyList<string> keys, string appName,
        string entityGuid, string transactionId, string traceId)
    {
        if (keys == null || keys.Count == 0)
            return string.Empty;

        var parts = new List<string>(keys.Count);
        foreach (var key in keys)
        {
            var value = key switch
            {
                "nr_service"      => appName,
                "nr_service_guid" => entityGuid,
                "nr_txn"          => transactionId,
                "nr_trace_id"     => traceId,
                _                 => null
            };

            if (string.IsNullOrEmpty(value) || value.Contains("*/"))
                continue;

            parts.Add($"{key}=\"{value}\"");
        }

        return parts.Count == 0 ? string.Empty : $"/*{string.Join(",", parts)}*/";
    }

    public static string PrependCommentToSql(string sql, string comment)
    {
        if (string.IsNullOrEmpty(comment) || sql.StartsWith("/*nr_"))
            return sql;

        return comment + sql;
    }
}
