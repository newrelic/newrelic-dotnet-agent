// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;

namespace NewRelic.Parsing
{

    /// <summary>
    /// Extracts features from SQL statement that are used to make a metric name.
    ///
    /// This uses ad-hoc scanning techniques.
    /// The scanner uses simple regular expressions.
    /// The scanner must be fast, as it is called for every SQL statement executed in the profiled code.
    /// The scanner is not a full parser; there are many constructs it can not handle, such as sequential statements (;),
    /// and the scanner has been extended in an ad-hoc manner as the need arises.
    /// 
    /// Database tracing is one of our largest sources of agent overhead.
    /// The issue is that many applications issue hundreds or thousands of database queries per transaction,
    /// so our db tracers are invoked much more often then other tracers.
    /// Our database tracers are also usually doing a lot more than other tracers,
    /// like parsing out SQL statements.  Just tread carefully with that in mind when making changes here.
    /// 
    /// When it comes to it, most users aren't going to want us to do really sophisticated sql parsing
    /// if it comes at the expense of increased overhead.
    /// </summary>
    public static class SqlParser
    {
        private const RegexOptions PatternSwitches = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline;
        private static readonly ParseStatement _statementParser;
        private static readonly ConcurrentDictionary<DatastoreVendor, ParsedSqlStatement> _nullParsedStatementStore = new ConcurrentDictionary<DatastoreVendor, ParsedSqlStatement>();

        private const string SqlParamPrefix = "@";
        private const char SemiColon = ';';

        // Regex Phrases
        private const string SelectPhrase = @"^\bselect\b.*?\s+";
        private const string InsertPhrase = @"^insert\s+into\s+";
        private const string UpdatePhrase = @"^update\s+";
        private const string DeletePhrase = @"^delete\s+";
        private const string CreatePhrase = @"^create\s+";
        private const string DropPhrase = @"^drop\s+";
        private const string AlterPhrase = @"^alter\s+";
        private const string CallPhrase = @"^call\s+";
        private const string SetPhrase = @"^set\s+@?";
        private const string DeclarePhrase = @"^declare\s+@?";

        // Shortcut phrases.  Parsers determine if they are applicable by checking the start of a cleaned version of the statement
        // for specific keywords.  If they are deemed applicable, the more expensive regEx is run against the statement to
        // extract the information and build the ParsedSQLStatement.
        private const string InsertPhraseShortcut = "insert";
        private const string UpdatePhraseShortcut = "update";
        private const string DeletePhraseShortcut = "delete";
        private const string CreatePhraseShortcut = "create";
        private const string DropPhraseShortcut = "drop";
        private const string AlterPhraseShortcut = "alter";
        private const string CallPhraseShortcut = "call";
        private const string SetPhraseShortcut = "set";
        private const string DeclarePhraseShortcut = "declare";
        private const string ExecuteProcedure1Shortcut = "exec";
        private const string ExecuteProcedure2Shortcut = "execute";
        private const string ExecuteProcedure3Shortcut = "sp_";
        private const string ShowPhraseShortcut = "show";
        private const string WaitforPhraseShortcut = "waitfor";

        // Regex to match only single SQL statements (i.e. no semicolon other than at the end)
        private const string SingleSqlStatementPhrase = @"^[^;]*[\s;]*$";

        private const string CommentPhrase = @"/\*.*?\*/"; //The ? makes the searching lazy
        private const string QueryNamePhrase = @"/\* *NewRelicQueryName: *(.*?)\*/"; //The ? makes the searching lazy
        private const string LeadingSetPhrase = @"^(?:\s*\bset\b.+?\;)+(?!(\s*\bset\b))";
        private const string StartObjectNameSeparator = @"[\s\(\[`\""]*";
        private const string EndObjectNameSeparator = @"[\s\)\]`\""]*";
        private const string ValidObjectName = @"([^,;\[\s\]\(\)`\""\.]*)";
        private const string FromPhrase = @"from\s+";
        private const string VariableNamePhrase = @"([^\s(=,]*).*";
        private const string ObjectTypePhrase = @"([^\s]*)";
        private const string CallObjectPhrase = @"([^\s(,]*).*";
        private const string MetricNamePhrase = @"^[a-z0-9.\$_]*$";

        // Regex Strings
        private const string SelectString = SelectPhrase + FromPhrase + @"(?:" + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")(?:\." + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")*";
        private const string InsertString = InsertPhrase + @"(?:" + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")(?:\." + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")*";
        private const string UpdateString = UpdatePhrase + @"(?:" + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")(?:\." + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")*";
        private const string DeleteString = DeletePhrase + "(" + FromPhrase + @")?(?:" + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")(?:\." + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")*";
        private const string CreateString = CreatePhrase + ObjectTypePhrase;
        private const string DropString = DropPhrase + ObjectTypePhrase;
        private const string AlterString = AlterPhrase + ObjectTypePhrase + ".*";
        private const string CallString = CallPhrase + CallObjectPhrase;
        private const string SetString = SetPhrase + VariableNamePhrase;
        private const string DeclareString = DeclarePhrase + VariableNamePhrase;

        private static readonly Regex CommentPattern = new Regex(CommentPhrase, RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex QueryNamePattern = new Regex(QueryNamePhrase, RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex LeadingSetPattern = new Regex(LeadingSetPhrase, RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
        private static readonly Regex ValidMetricNameMatcher = new Regex(MetricNamePhrase, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SingleSqlStatementMatcher = new Regex(SingleSqlStatementPhrase, PatternSwitches);
        private static readonly Regex SelectRegex = new Regex(SelectString, PatternSwitches);
        private static readonly Regex InsertRegex = new Regex(InsertString, PatternSwitches);
        private static readonly Regex UpdateRegex = new Regex(UpdateString, PatternSwitches);
        private static readonly Regex DeleteRegex = new Regex(DeleteString, PatternSwitches);
        private static readonly Regex CreateRegex = new Regex(CreateString, PatternSwitches);
        private static readonly Regex DropRegex = new Regex(DropString, PatternSwitches);
        private static readonly Regex AlterRegex = new Regex(AlterString, PatternSwitches);
        private static readonly Regex CallRegex = new Regex(CallString, PatternSwitches);
        private static readonly Regex SetRegex = new Regex(SetString, PatternSwitches);
        private static readonly Regex DeclareRegex = new Regex(DeclareString, PatternSwitches);
        private static readonly Regex ExecuteProcedureRegex1 = new Regex(@"^exec\s+(?:[^\s=]+\s*=\s*)?([^\s(,;]+)", PatternSwitches);
        private static readonly Regex ExecuteProcedureRegex2 = new Regex(@"^execute\s+(?:[^\s=]+\s*=\s*)?([^\s(,;]+)", PatternSwitches);
        private static readonly Regex ExecuteProcedureRegex3 = new Regex(@"^(sp_\s*[^\s]*).*", PatternSwitches);

        /// <summary>
        /// Heursitical crawl through a database command to find features suitable for making metric names.
        /// This uses linear search through the regexp patterns.
        /// </summary>
        /// <returns>A ParsedDatabaseStatement if some heuristic matches; otherwise null</returns>
        public static ParsedSqlStatement GetParsedDatabaseStatement(DatastoreVendor datastoreVendor, CommandType commandType, string commandText)
        {
            try
            {
                switch (commandType)
                {
                    case CommandType.TableDirect:
                        return new ParsedSqlStatement(datastoreVendor, commandText, "select");
                    case CommandType.StoredProcedure:
                        return new ParsedSqlStatement(datastoreVendor, StringsHelper.FixDatabaseObjectName(commandText), "ExecuteProcedure");
                }

                var explicitModel = GetExplicitModel(commandText);

                // Remove comments.
                var statement = CommentPattern.Replace(commandText, string.Empty).TrimStart();

                if (!IsSingleSqlStatement(statement))
                {
                    // Remove leading SET commands

                    // Trimming any trailing semicolons is necessary to avoid having the LeadingSetPattern
                    // match a SQL statement that ONLY contains SET commands, which would leave us with nothing
                    statement = statement.TrimEnd(SemiColon);
                    statement = LeadingSetPattern.Replace(statement, string.Empty).TrimStart();
                }

                return _statementParser(datastoreVendor, commandType, commandText, statement, explicitModel);
            }
            catch
            {
                return new ParsedSqlStatement(datastoreVendor, null, null);
            }
        }

        private static string GetExplicitModel(string commandText)
        {
            var match = QueryNamePattern.Match(commandText);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            return null;
        }

        public static bool IsValidName(string name)
        {
            return ValidMetricNameMatcher.IsMatch(name);
        }

        public static bool IsSingleSqlStatement(string sql)
        {
            return SingleSqlStatementMatcher.IsMatch(sql);
        }

        private static readonly HashSet<string> _operations = new HashSet<string>();
        public static IEnumerable<string> Operations => _operations;

        static SqlParser()
        {
            // as for example
            //   ... FROM Northwind.dbo.[Order Details] AS OrdDet ...
            // This was first noticed for the SELECT ... FROM statements from the Northwind example DB.

            // Order these statement parsers in descending order of frequency of use.
            // We'll do a linear search through the table to find the appropriate matcher.
            _statementParser = CreateCompoundStatementParser(

                // selects are tricky with the set crap on the front of the statement.. /* fooo */set
                // leave those out of the dictionary
                new DefaultStatementParser("select", SelectRegex, string.Empty).ParseStatement,
                new ShowStatementParser().ParseStatement,
                new DefaultStatementParser("insert", InsertRegex, InsertPhraseShortcut).ParseStatement,
                new DefaultStatementParser("update", UpdateRegex, UpdatePhraseShortcut).ParseStatement,
                new DefaultStatementParser("delete", DeleteRegex, DeletePhraseShortcut).ParseStatement,

                new DefaultStatementParser("ExecuteProcedure", ExecuteProcedureRegex1, ExecuteProcedure1Shortcut).ParseStatement,
                new DefaultStatementParser("ExecuteProcedure", ExecuteProcedureRegex2, ExecuteProcedure2Shortcut).ParseStatement,
                // Invocation of a conventionally named stored procedure.
                new DefaultStatementParser("ExecuteProcedure", ExecuteProcedureRegex3, ExecuteProcedure3Shortcut).ParseStatement,

                new DefaultStatementParser("create", CreateRegex, CreatePhraseShortcut).ParseStatement,
                new DefaultStatementParser("drop", DropRegex, DropPhraseShortcut).ParseStatement,
                new DefaultStatementParser("alter", AlterRegex, AlterPhraseShortcut).ParseStatement,
                new DefaultStatementParser("call", CallRegex, CallPhraseShortcut).ParseStatement,

                // See http://msdn.microsoft.com/en-us/library/ms189484.aspx
                // The set statement targets a local identifier whose name may start with @.  We just scan over the @.
                new DefaultStatementParser("set", SetRegex, SetPhraseShortcut).ParseStatement,

                // See http://msdn.microsoft.com/en-us/library/ms188927.aspx
                // The declare statement targets a local identifier whose name may start with @.  We just scan over the @.
                new DefaultStatementParser("declare", DeclareRegex, DeclarePhraseShortcut).ParseStatement,

                new SelectVariableStatementParser().ParseStatement,

                // The Waitfor statement is [probably] only in Transact SQL.  There are multiple variations of the statement.
                // See http://msdn.microsoft.com/en-us/library/ms187331.aspx
                new WaitforStatementParser().ParseStatement);
        }

        private delegate ParsedSqlStatement ParseStatement(DatastoreVendor datastoreVendor, CommandType commandType, string commandText, string statement, string explicitModel);

        private static ParseStatement CreateCompoundStatementParser(params ParseStatement[] parsers)
        {
            //The parsers params are used inside a return function that is referenced by a static class member.
            //This effectively makes theses parsers stay with agent entire time.
            return (datastoreVendor, commandType, commandText, statement, explicitModel) =>
            {
                foreach (var parser in parsers)
                {
                    var parsedStatement = parser(datastoreVendor, commandType, commandText, statement, explicitModel);
                    if (parsedStatement != null)
                    {
                        return parsedStatement;
                    }
                }

                return _nullParsedStatementStore.GetOrAdd(datastoreVendor, x => new ParsedSqlStatement(datastoreVendor, null, null));
            };
        }

        public class DefaultStatementParser
        {
            private readonly Regex _pattern;
            private readonly string _shortcut;
            private readonly string _key;

            public DefaultStatementParser(string key, Regex pattern, string shortcut)
            {
                _key = key;
                _pattern = pattern;

                if (!string.IsNullOrEmpty(shortcut))
                {
                    _shortcut = shortcut;
                }

                _operations.Add(key);
            }

            public virtual ParsedSqlStatement ParseStatement(DatastoreVendor vendor, CommandType commandType, string commandText, string statement, string explicitModel)
            {
                if (!string.IsNullOrEmpty(_shortcut) && !statement.StartsWith(_shortcut, StringComparison.CurrentCultureIgnoreCase))
                {
                    return null;
                }

                var matcher = _pattern.Match(statement);
                if (!matcher.Success)
                    return null;

                var model = "unknown";
                foreach (Group g in matcher.Groups)
                {
                    var str = g.ToString();
                    if (!string.IsNullOrEmpty(str))
                    {
                        model = str;
                    }
                }

                if (string.Equals(model, "select", StringComparison.CurrentCultureIgnoreCase))
                {
                    model = "(subquery)";
                }
                else
                {
                    model = StringsHelper.FixDatabaseObjectName(model);
                    if (!IsValidModelName(model))
                    {
                        model = "ParseError";
                    }
                }
                
                return CreateParsedDatabaseStatement(vendor, GetFullModel(model, explicitModel));
            }

            protected virtual bool IsValidModelName(string name)
            {
                return IsValidName(name);
            }

            protected virtual ParsedSqlStatement CreateParsedDatabaseStatement(DatastoreVendor vendor, string model)
            {
                return new ParsedSqlStatement(vendor, model.ToLower(), _key);
            }
        }

        private class SelectVariableStatementParser
        {
            private static readonly Regex SelectMatcher = new Regex(@"^select\s+([^\s,]*).*", PatternSwitches);
            private static readonly Regex FromMatcher = new Regex(@"\s+from\s+", PatternSwitches);

            public ParsedSqlStatement ParseStatement(DatastoreVendor vendor, CommandType commandType, string commandText, string statement, string explicitModel)
            {
                var matcher = SelectMatcher.Match(statement);
                if (matcher.Success)
                {
                    string model = FromMatcher.Match(statement).Success
                        ? "(subquery)"
                        : "VARIABLE";
                    
                    return new ParsedSqlStatement(vendor, GetFullModel(model, explicitModel), "select");
                }
                return null;
            }

        }

        private class ShowStatementParser : DefaultStatementParser
        {
            public ShowStatementParser() : base("show", new Regex(@"^\s*show\s+(.*)$", PatternSwitches), ShowPhraseShortcut)
            {
            }

            protected override bool IsValidModelName(string name)
            {
                return true;
            }

            protected override ParsedSqlStatement CreateParsedDatabaseStatement(DatastoreVendor vendor, string model)
            {
                if (model.Length > 50)
                {
                    model = model.Substring(0, 50);
                }
                return new ParsedSqlStatement(vendor, model, "show");
            }
        }

        /// <summary>
        /// The Waitfor statement is [probably] only in Transact SQL.  There are multiple variations of the statement.
        /// See https://docs.microsoft.com/en-us/sql/t-sql/language-elements/waitfor-transact-sql
        /// </summary>
        private class WaitforStatementParser : DefaultStatementParser
        {


            public WaitforStatementParser() : base("waitfor", new Regex(@"^waitfor\s+(delay|time)\s+([^\s,(;]*).*", PatternSwitches), WaitforPhraseShortcut)
            {
            }

            // All time stamps we match with the Regex are assumed to be valid "names" for our purpose.
            protected override bool IsValidModelName(string name)
            {
                return true;
            }

            protected override ParsedSqlStatement CreateParsedDatabaseStatement(DatastoreVendor vendor, string model)
            {
                // We drop the time string in this.model on the floor.  It may contain quotes, colons, periods, etc.
                return new ParsedSqlStatement(vendor, "time", "waitfor");
            }
        }

        /// <summary>
        /// This method takes any parameters in the given command and plugs them back into the parameterized command text.
        /// </summary>
        /// <param name="command">SQL command to parse</param>
        /// <returns>True if the plan should be executed.  False if the plan should be aborted.</returns>
        public static bool FixParameterizedSql(IDbCommand command)
        {
            if (command.CommandType == CommandType.StoredProcedure)
            {
                return true;
            }

            if (command.Parameters.Count == 0)
            {
                return true; // no params, using raw sql statement.
            }

            var sql = command.CommandText;
            foreach (object parameter in command.Parameters)
            {
                var dbParam = (IDbDataParameter)parameter;
                //DebugParam(dbParam, sqlObfuscator);

                // Binary: We cannot determine exactly what is in the binary so the out could be invalid for SQL.
                // Object: Object could be almost anything so there is no easy way ToString it that just works without trying to detect all the differnt objects
                // It is safer, less error prone, and more efficient to short-circuit on these types than to try and parse them out.
                var type = dbParam.DbType;
                if (type == DbType.Binary)
                {
                    Log.Debug("Not executing explain plan since DbType is Binary.");
                    return false;
                }
                else if (type == DbType.Object)
                {
                    Log.Debug("Not executing explain plan since DbType is Object.");
                    return false;
                }
                
                // Parameter names can be supplied with the prefix @ or without
                // if is supplied, remove the @ to the beginning of the param name
                // This is to deal with regex issues, see below
                var paramName = dbParam.ParameterName;
                if (paramName.StartsWith(SqlParamPrefix))
                {
                    paramName = paramName.Replace(SqlParamPrefix, ""); ;
                }

                // The following should be handled by a defualt .ToString() to get the numbers as a string
                // Byte, Decimal, Double, Int16, Int32, Int64, SByte, Single, UInt16, UInt32, UInt64, VarNumeric, Currency
                object value = dbParam.Value;
                if (quotableTypes.Contains(type.ToString()))  // the TypeCode for Strings is Int32 for some reason
                {
                    value = QuoteString(value.ToString());
                }
                else if (type == DbType.Boolean)  // value must be type bool
                {
                    value = ((bool)value) ? 1 : 0;
                }

                // Regex doesn't consider the @ to be a word so we have to add the @ before the word boundery match "\b"
                // This is why we strip the @ from the param before using it in the regex.
                sql = Regex.Replace(sql, $@"@\b{paramName}\b", value.ToString());
            }

            command.Parameters.Clear();
            command.CommandText = sql;
            return true;
        }

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

        private static string QuoteString(string str)
        {
            //All single quotes in the string are replaced by two singe quotes as sql server doesn't parse single quotes in strings. 
            return "'" + str.Replace("'", "''") + "'";
        }

        private static string GetFullModel(string baseModel, string explicitModel)
        {
            if (explicitModel == null)
            {
                return baseModel;
            }

            // Forward slashes will cause the incorrect thing to be registered as the "operation".
            // Change them to pipes. If the user doesn't like that, they can do their own replacement.
            string escapedExplicitModel = explicitModel.Replace("/", "|");

            return $"{baseModel} - [{escapedExplicitModel}]";
        }
    }
}
