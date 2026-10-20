// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Data;

namespace NewRelic.Agent.Extensions.Parsing;

public static class SqlMetadataCommentBuilder
{
    private const string CommentStart = "/*nr_service_guid=";
    private const string CommentPrefix = CommentStart + "\"";
    private const string CommentSuffix = "\"*/";
    private const int MaxGuidLength = 128;

    public static string BuildComment(string entityGuid)
    {
        if (string.IsNullOrEmpty(entityGuid) || entityGuid.Contains("*/"))
            return string.Empty;

        return CommentPrefix + entityGuid + CommentSuffix;
    }

    public static string PrependCommentToSql(string sql, string comment)
    {
        if (string.IsNullOrEmpty(comment) || sql.StartsWith(CommentStart, StringComparison.Ordinal))
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

    /// <summary>
    /// Returns the length of a well-formed metadata comment (exactly what BuildComment produces) at the very start of
    /// the SQL, or 0 if there is none. SQL obfuscation keeps this prefix verbatim so the commented form stays visible
    /// in traces, as the SQL-Metadata-Comments spec requires. Because the comment is kept verbatim, the entity GUID is
    /// restricted to what a real one looks like (1 to 128 base64-style characters); anything else is not treated as ours.
    /// </summary>
    public static int GetMetadataCommentLength(string sql)
    {
        if (sql == null || !sql.StartsWith(CommentPrefix, StringComparison.Ordinal))
            return 0;

        var searchLength = Math.Min(MaxGuidLength + CommentSuffix.Length, sql.Length - CommentPrefix.Length);
        var suffixIndex = sql.IndexOf(CommentSuffix, CommentPrefix.Length, searchLength, StringComparison.Ordinal);
        if (suffixIndex <= CommentPrefix.Length)
            return 0;

        for (var i = CommentPrefix.Length; i < suffixIndex; i++)
        {
            if (!IsGuidCharacter(sql[i]))
                return 0;
        }

        return suffixIndex + CommentSuffix.Length;
    }

    private static bool IsGuidCharacter(char ch)
    {
        return (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')
               || ch == '+' || ch == '/' || ch == '=' || ch == '_' || ch == '-';
    }
}
