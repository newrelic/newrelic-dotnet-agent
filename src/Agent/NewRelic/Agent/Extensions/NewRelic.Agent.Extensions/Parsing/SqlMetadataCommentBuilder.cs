// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Data;

namespace NewRelic.Agent.Extensions.Parsing;

public static class SqlMetadataCommentBuilder
{
    public static string BuildComment(string entityGuid)
    {
        if (string.IsNullOrEmpty(entityGuid) || entityGuid.Contains("*/"))
            return string.Empty;

        return $"/*nr_service_guid=\"{entityGuid}\"*/";
    }

    public static string PrependCommentToSql(string sql, string comment)
    {
        if (string.IsNullOrEmpty(comment) || sql.StartsWith("/*nr_service_guid="))
            return sql;

        return comment + sql;
    }

    public static bool ShouldSkipCommentForCommandType(CommandType commandType)
    {
        // CommandType.StoredProcedure means CommandText is a literal object identifier passed
        // straight to the driver's RPC call, not parsed SQL. Prepending a comment corrupts the
        // identifier and breaks the call ("Could not find stored procedure").
        return commandType == CommandType.StoredProcedure;
    }
}
