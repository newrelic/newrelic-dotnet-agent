// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text;
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
    /// Replaces literals with "?". Postgres uses the spec-compliant SqlLiteralScanner; the remaining vendors still use the
    /// legacy finite state machine below. The agent's own leading SQL metadata comment is kept verbatim for every vendor.
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
            // Both obfuscators write the comment and the obfuscated body into one buffer and truncate it, so a huge statement
            // is not copied again for the body, the concatenation or the truncation.
            var commentLength = SqlMetadataCommentBuilder.GetMetadataCommentLength(sql);
            var comment = commentLength == 0 ? null : sql.Substring(0, commentLength);

            return vendor == DatastoreVendor.Postgres
                ? SqlLiteralScanner.Obfuscate(sql, commentLength, comment, SqlStatementMaxLength, SqlDialects.Postgres)
                : ObfuscateLegacy(sql, commentLength, comment, vendor);
        }

        /// <summary>
        /// Does not use regular expressions, but a finite state machine that is easier to demonstrate always makes progress.
        /// Returns prefix + the obfuscated sql from startIndex, truncated to SqlStatementMaxLength. The output only ever
        /// grows, so the scan stops once it reaches the limit.
        /// </summary>
        private static string ObfuscateLegacy(string sql, int startIndex, string prefix, DatastoreVendor vendor)
        {
            StringBuilder sb = new StringBuilder(prefix, Math.Min(sql.Length - startIndex + (prefix?.Length ?? 0), SqlStatementMaxLength));
            int length = sql.Length;
            for (int i = startIndex; i < length && sb.Length < SqlStatementMaxLength; i++)
            {
                char ch = sql[i];

                // Span across quoted strings.
                if (ch == '\'' || ch == '"' || ch == '`')
                {
                    char quotechar = ch;
                    sb.Append('?');
                    i += 1;  // Skip into string
                    for (; i < length; i++)
                    {
                        ch = sql[i];

                        // MS SQL Server has different escaping rules by default than most other vendors
                        // In particular, backslashes do not escape the next character, so they should not be treated specially
                        if (vendor == DatastoreVendor.MSSQL && ch == quotechar)
                        {
                            // In order to get a literal single quote inside of a single-quoted string, MSSQL uses two single quotes in a row, so we need to check for this case
                            // This implementation works no matter what the quoting character happens to be
                            if (i < length - 1 && sql[i + 1] == quotechar)
                            {
                                i += 1;
                                continue;
                            }
                            break;
                        }

                        if (vendor != DatastoreVendor.MSSQL && ch == '\\' && i < length - 1)
                        {
                            // Skip escaped characters
                            i += 1;
                            continue;
                        }
                        if (ch == quotechar)
                        {
                            break;
                        }
                    }
                    if (i >= length) break;  // Fell off the end
                    // We've reached the termination character of the string, which we'll implicitly consume in the outer loop
                    continue;
                }

                // Span across numeric values, including floats.
                // We're a little lazy here, and allow a single number to have multiple decimal points.
                // But we know we are dealing with well formed input.
                if (char.IsDigit(ch) || (ch == '.' && i < length - 1 && char.IsDigit(sql[i + 1])))
                {
                    sb.Append('?');
                    for (; i < length; i++)
                    {
                        ch = sql[i];
                        if (char.IsDigit(ch) || (ch == '.'))
                        {
                            continue;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (i >= length) break;  // Fell off the end
                    i -= 1;  // back up to just before failure
                    continue;
                }

                // Span across identifiers
                if (char.IsLetter(ch) || ch == '_')
                {
                    for (; i < length; i++)
                    {
                        ch = sql[i];
                        if (char.IsLetter(ch) || ch == '_' || char.IsDigit(ch))
                        {
                            sb.Append(ch);
                            continue;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (i >= length) break;  // Fell off the end
                    i -= 1;  // back up to just before failure
                    continue;
                }

                // None of the above, just pass it through
                sb.Append(ch);
            }

            return sb.ToString(0, Math.Min(sb.Length, SqlStatementMaxLength));
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