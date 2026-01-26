// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text;
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
    /// Replaces strings and numeric values with a simple "?".
    /// Does not use regular expressions, but a finite state machine that is easier to demonstrate always makes progress.
    /// </summary>
    class ObfuscatingSqlObfuscatorUsingExplicit : SqlObfuscator
    {
        public override string GetObfuscatedSql(string sql, DatastoreVendor vendor = DatastoreVendor.Other)
        {
            if (sql == null)
            {
                return null;
            }

            StringBuilder sb = new StringBuilder();
            int length = sql.Length;
            for (int i = 0; i < length; i++)
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