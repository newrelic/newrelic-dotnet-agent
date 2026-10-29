// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Database;

public abstract class SqlObfuscator
{
    public abstract string GetObfuscatedSql(string sql, DatastoreVendor vendor = DatastoreVendor.Other);

    private const string ObfuscatedSetting = "obfuscated";
    private const string RawSetting = "raw";
    private const string OffSetting = "off";
    private const int SqlStatementMaxLength = 16384;

    private static readonly SqlObfuscator ObfuscatingSqlObfuscatorInstanceUsingExplicit = new ObfuscatingSqlObfuscatorUsingExplicit();
    private static readonly SqlObfuscator RawSqlObfuscatorInstance = new RawSqlObfuscator();
    private static readonly SqlObfuscator NoSqlObfuscatorInstance = new NoSqlObfuscator();

    public static SqlObfuscator GetObfuscatingSqlObfuscator()
    {
        return ObfuscatingSqlObfuscatorInstanceUsingExplicit;
    }

    public static SqlObfuscator GetSqlObfuscator(string recordSqlValue)
    {
        if (string.IsNullOrEmpty(recordSqlValue))
        {
            return GetObfuscatingSqlObfuscator();
        }

        if (recordSqlValue.Equals(OffSetting, StringComparison.InvariantCultureIgnoreCase))
        {
            return NoSqlObfuscatorInstance;
        }
        else if (recordSqlValue.Equals(RawSetting, StringComparison.InvariantCultureIgnoreCase))
        {
            return RawSqlObfuscatorInstance;
        }
        else if (recordSqlValue.Equals(ObfuscatedSetting, StringComparison.InvariantCultureIgnoreCase))
        {
            return GetObfuscatingSqlObfuscator();
        }
        return GetObfuscatingSqlObfuscator();
    }

    /// <summary>
    /// Obfuscates one explain-plan cell. SQL Server SHOWPLAN_ALL child rows start with a tree prefix such as "  |--" or
    /// "       |    |--". Its "--" is not a comment, so the prefix (spaces, tabs, pipes and the "--" after the last pipe,
    /// which cannot carry a literal) is kept verbatim and only the operator text after it is obfuscated.
    /// Returns null when the obfuscator returns null (record_sql off).
    /// </summary>
    public virtual string GetObfuscatedExplainPlanValue(string value, DatastoreVendor vendor)
    {
        if (value == null)
        {
            return null;
        }

        var prefixLength = ShowPlanTreePrefixLength(value);
        if (prefixLength == 0)
        {
            return GetObfuscatedSql(value, vendor);
        }

        var obfuscated = GetObfuscatedSql(value.Substring(prefixLength), vendor);
        return obfuscated == null ? null : value.Substring(0, prefixLength) + obfuscated;
    }

    private static int ShowPlanTreePrefixLength(string value)
    {
        var j = 0;
        while (j < value.Length && (value[j] == ' ' || value[j] == '\t' || value[j] == '|'))
        {
            j++;
        }

        var isTreePrefix = j > 0 && value[j - 1] == '|' && j + 1 < value.Length && value[j] == '-' && value[j + 1] == '-';
        return isTreePrefix ? j + 2 : 0;
    }

    /// <summary>
    /// Completely consume any SQL and don't expose anything.
    /// </summary>
    class NoSqlObfuscator : SqlObfuscator
    {
        public override string GetObfuscatedSql(string sql, DatastoreVendor vendor = DatastoreVendor.Other)
        {
            return null;
        }
    }

    /// <summary>
    /// Replaces literals with "?" using the vendor's SQL dialect rules (see SqlLiteralScanner). The agent's own leading SQL
    /// metadata comment is kept verbatim.
    /// </summary>
    class ObfuscatingSqlObfuscatorUsingExplicit : SqlObfuscator
    {
        public override string GetObfuscatedSql(string sql, DatastoreVendor vendor = DatastoreVendor.Other)
        {
            if (sql == null)
            {
                return null;
            }

            // Keep the agent's metadata comment so the commented form stays visible in traces (SQL-Metadata-Comments spec).
            // The scanner writes the comment and the obfuscated body into one buffer and truncates it, so a huge statement
            // is not copied again for the body, the concatenation or the truncation.
            var commentLength = SqlMetadataCommentBuilder.GetMetadataCommentLength(sql);
            var comment = commentLength == 0 ? null : sql.Substring(0, commentLength);

            return SqlLiteralScanner.Obfuscate(sql, commentLength, comment, SqlStatementMaxLength, SqlDialects.ForVendor(vendor));
        }
    }

    /// <summary>
    /// Don't do any SQL obfuscation: just return the raw string.
    /// </summary>
    class RawSqlObfuscator : SqlObfuscator
    {
        public override string GetObfuscatedSql(string sql, DatastoreVendor vendor = DatastoreVendor.Other)
        {
            return sql;
        }
    }
}