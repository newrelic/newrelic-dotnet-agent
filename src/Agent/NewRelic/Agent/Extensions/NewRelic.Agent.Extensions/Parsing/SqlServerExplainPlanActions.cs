// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Extensions.Parsing;

public static class SqlServerExplainPlanActions
{
    private const string SqlParamPrefix = "@";

    private static readonly List<string> quotableTypes = new List<string>() {
        "String",
        "StringFixedLength",
        "DateTime",
        "DateTime2",
        "Date",
        "Time",
        "AnsiString",
        "AnsiStringFixedLength",
        "Xml",
        "Guid",
        "DateTimeOffset",
    };

    /// <param name="resources">The cloned <see cref="IDbCommand"/> (and its cloned connection) to explain.</param>
    /// <param name="stripSqlComments">
    /// Strips comments from a SQL string (see <see cref="InlineParametersForCompileOnlyExplain"/> for why this
    /// matters). Supplied by the caller because the comment/string-literal-aware scanner that implements this
    /// lives in Core, which this project cannot reference (Core references Extensions, never the reverse) --
    /// <c>IAgentExperimental.DatabaseService.StripSqlComments</c> is the concrete implementation in practice.
    /// </param>
    public static ExplainPlan GenerateExplainPlan(object resources, Func<string, string> stripSqlComments)
    {
        if (!(resources is IDbCommand))
        {
            return null;
        }

        ExplainPlan explainPlan = null;

        //KILL THE CONNECTION NO MATTER WHAT HAPPENS DURING EXECUTION TO AVOID CONNECTION LEAKING
        var dbCommand = (IDbCommand)resources;
        using (dbCommand)
        using (dbCommand.Connection)
        {
            var shouldGeneratePlan = InlineParametersForCompileOnlyExplain(dbCommand, stripSqlComments);
            if (!shouldGeneratePlan)
            {
                return explainPlan;
            }

            if (dbCommand.Connection.State != ConnectionState.Open)
            {
                dbCommand.Connection.Open();
            }

            SetShowPlan(dbCommand.Connection, true);

            try
            {
                using (IDataReader reader = dbCommand.ExecuteReader())
                {
                    var headers = GetReaderHeaders(reader);
                    // Decide which headers should be obfuscated based on the Vendor (this is only SQL)
                    var obfuscatedHeaders = GetObfuscatedIndexes(headers);
                    explainPlan = new ExplainPlan(headers, new List<List<object>>(), obfuscatedHeaders);

                    var explainPlanDatas = new List<List<object>>();
                    while (reader.Read())
                    {
                        object[] values = new object[reader.FieldCount];
                        reader.GetValues(values);
                        explainPlanDatas.Add(values.ToList());
                    }

                    explainPlan.ExplainPlanDatas = explainPlanDatas;

                }
            }
            finally
            {
                // Always attempt to turn SHOWPLAN back off, even if ExecuteReader or reading the results
                // failed -- otherwise the exception would leave the cloned connection's session with
                // SHOWPLAN_ALL still ON when it's disposed. Swallow a failure here so it doesn't mask the
                // original exception; the connection is about to be disposed either way.
                try
                {
                    SetShowPlan(dbCommand.Connection, false);
                }
                catch
                {
                }
            }
        }

        return explainPlan;
    }

    /// <summary>
    /// SET SHOWPLAN_ALL ON only intercepts plain-text T-SQL batches -- it cannot see a statement submitted as an
    /// RPC call, which is what Microsoft.Data.SqlClient / System.Data.SqlClient always does for any command that
    /// still has bound parameters. So this method plugs the clone's parameter values back into the batch text,
    /// exactly as the pre-fix code once did, but using a single regex pass with a MatchEvaluator rather than
    /// sequential text substitution:
    ///  - A MatchEvaluator's return value is used completely literally; unlike the string overload of
    ///    Regex.Replace, "$&amp;"/"$1"-style sequences in a parameter's value are never reinterpreted.
    ///  - Regex.Replace scans its input exactly once, left to right, and never re-examines text it has already
    ///    written. Matching every parameter name in one combined alternation means one parameter's inlined value
    ///    can never be mistaken for another parameter's placeholder, because the substituted text is never
    ///    rescanned.
    /// Type-specific literal formatting (which types get quoted, how booleans and unrecognized types are
    /// handled) intentionally matches the original code for now; only the substitution mechanism and null
    /// handling have changed here.
    ///
    /// Known limitations, deliberately not addressed yet: numeric and date/time values are formatted with the
    /// current thread culture (via plain .ToString()), not CultureInfo.InvariantCulture, so a non-'.'-decimal or
    /// ambiguous-date-format culture on the agent process could produce an incorrect literal. And DbType handling
    /// here is not an exhaustive, fail-closed switch over every DbType value -- only the types this method
    /// explicitly recognizes get formatted; anything else falls into the naive ToString() branch below rather
    /// than being rejected. Revisit both before relying on this for exotic parameter types or non-US-culture
    /// agent deployments.
    ///
    /// Comments are stripped from the command text (via <paramref name="stripSqlComments"/>) before the
    /// placeholder regex runs, since placeholder-looking text inside a comment should not be treated as a
    /// placeholder to fill in. A null result from <paramref name="stripSqlComments"/> means the command text
    /// could not be classified with confidence, so the plan is aborted rather than matched against untrustworthy
    /// text.
    /// </summary>
    /// <param name="command">SQL Server command to parse.</param>
    /// <param name="stripSqlComments">See <see cref="GenerateExplainPlan"/>.</param>
    /// <returns>True if the plan should be executed. False if the plan should be aborted.</returns>
    public static bool InlineParametersForCompileOnlyExplain(IDbCommand command, Func<string, string> stripSqlComments)
    {
        if (command.CommandType == CommandType.StoredProcedure)
        {
            return true;
        }

        if (command.Parameters.Count == 0)
        {
            return true; // no params, using raw sql statement.
        }

        // SQL Server parameter names are case-insensitive, so @UserId in the command text must match a parameter
        // declared as @userid.
        var literalsByParamName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (object parameter in command.Parameters)
        {
            var dbParam = (IDbDataParameter)parameter;

            var paramName = dbParam.ParameterName;
            if (paramName.StartsWith(SqlParamPrefix))
            {
                paramName = paramName.Substring(SqlParamPrefix.Length);
            }

            string literal;
            if (dbParam.Value == null || dbParam.Value == DBNull.Value)
            {
                literal = "NULL";
            }
            else
            {
                // Binary: we cannot determine exactly what is in the binary so the output could be invalid SQL.
                // Object: could be almost anything, so there is no reliable way to ToString() it safely.
                // It is safer, less error prone, and more efficient to short-circuit on these types than to try
                // and parse them out.
                var type = dbParam.DbType;
                if (type == DbType.Binary || type == DbType.Object)
                {
                    return false;
                }

                if (quotableTypes.Contains(type.ToString())) // the TypeCode for Strings is Int32 for some reason
                {
                    literal = QuoteString(dbParam.Value.ToString());
                }
                else if (type == DbType.Boolean) // value must be type bool
                {
                    literal = ((bool)dbParam.Value) ? "1" : "0";
                }
                else
                {
                    // Known gap (see method doc comment): naive ToString() on whatever DbType this is, using
                    // the current thread culture -- not CultureInfo.InvariantCulture. Correct for a plain-ASCII
                    // numeric on a '.'-decimal culture; not guaranteed correct otherwise, and not an exhaustive,
                    // fail-closed list of every DbType the way the Binary/Object skip above is.
                    literal = dbParam.Value.ToString();
                }
            }

            literalsByParamName[paramName] = literal;
        }

        // A null result means the we could not classify the text with confidence, so there is nothing safe to match against.
        var commentFreeCommandText = stripSqlComments(command.CommandText);
        if (commentFreeCommandText == null)
        {
            return false;
        }

        // One combined alternation, matched in a single Regex.Replace pass -- see the method doc comment for why
        // that single pass is what actually matters here, not just the MatchEvaluator overload on its own.
        // IgnoreCase matches SQL Server's case-insensitive parameter names; the dictionary lookup below is
        // case-insensitive for the same reason.
        var combinedPattern = @"@\b(?:" + string.Join("|", literalsByParamName.Keys.Select(Regex.Escape)) + @")\b";
        var sql = Regex.Replace(commentFreeCommandText, combinedPattern, match => literalsByParamName[match.Value.Substring(SqlParamPrefix.Length)], RegexOptions.IgnoreCase);

        command.Parameters.Clear();
        command.CommandText = sql;
        return true;
    }

    private static string QuoteString(string str)
    {
        //All single quotes in the string are replaced by two singe quotes as sql server doesn't parse single quotes in strings.
        return "'" + str.Replace("'", "''") + "'";
    }

    public static object AllocateResources(IDbCommand command)
    {
        if (!(command is ICloneable))
            return null;

        var clonedCommand = (IDbCommand)((ICloneable)command).Clone();
        var connection = (IDbConnection)((ICloneable)command.Connection).Clone();

        clonedCommand.Connection = connection;
        clonedCommand.Transaction = null;

        return clonedCommand;
    }

    public static List<int> GetObfuscatedIndexes(List<string> headers)
    {
        var indexes = new List<int>(ObfuscateFieldNames().Length);
        foreach (var field in ObfuscateFieldNames())
        {
            var index = headers.FindIndex(field.Equals);
            if (index >= 0)
            {
                indexes.Add(index);
            }
        }

        return indexes;
    }

    private static List<string> GetReaderHeaders(IDataReader reader)
    {
        List<string> headers = new List<string>(reader.FieldCount);
        for (int i = 0; i < reader.FieldCount; i++)
        {
            headers.Add(reader.GetName(i));
        }
        return headers;
    }

    private static string[] ObfuscateFieldNames()
    {
        return new[] {
            "StmtText",
            "Argument"
        };
    }

    private static void SetShowPlan(IDbConnection connection, bool on)
    {
        using (IDbCommand command = connection.CreateCommand())
        {
            command.CommandText = "SET SHOWPLAN_ALL " + (on ? "ON" : "OFF");
            command.ExecuteNonQuery();
        }
    }
}
