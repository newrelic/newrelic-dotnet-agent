using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using NewRelic.Agent.Extensions.Parsing;

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

        private const string SqlParamPrefix = "@";

        // Regex Phrases
        private const string SelectPhrase = @"(?=^/\*.*\*/*\s\bset\b.*;\s*\bselect\b|^/\*.*\*/*\s\bselect\b|^\bset\b.*;\s*\bselect\b|^\bselect\b).*?\s+";
        private const string InsertPhrase = @"^insert\s+into\s+";
        private const string UpdatePhrase = @"^update\s+";
        private const string DeletePhrase = @"^delete\s+";
        private const string CreatePhrase = @"^create\s+";
        private const string DropPhrase = @"^drop\s+";
        private const string AlterPhrase = @"^alter\s+";
        private const string CallPhrase = @"^call\s+";
        private const string SetPhrase = @"^set\s+@?";
        private const string DeclarePhrase = @"^declare\s+@?";

        // Regex to match only single SQL statements (i.e. no semicolon other than at the end - DOTNET-3029)
        private const string SingleSqlStatementPhrase = @"^[^;]*[\s;]*$";

        private const string CommentPhrase = @"/\*.*?\*/";
        private const string StartObjectNameSeparator = @"[\s\(\[`\""]*";
        private const string EndObjectNameSeparator = @"[\s\)\]`\""]*";
        // This doesn't catch spaces inside of object names, even if the names are surrounded by separators. [Table Name] would resolve to simply "Table".
        private const string ValidObjectName = @"([^,;\[\s\]\(\)`\""\.]*)";
        private const string FromPhrase = @"from\s+";
        private const string VariableNamePhrase = @"([^\s(=,]*).*";
        private const string ObjectTypePhrase = @"([^\s]*)";
        private const string CallObjectPhrase = @"([^\s(,]*).*";
        private const string MetricNamePhrase = @"^[a-z0-9.\$_]*$";

        // Regex Strings
        private const string SelectString = SelectPhrase + FromPhrase + @"(" + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")(\." + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")*";
        private const string InsertString = InsertPhrase + @"(" + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")(\." + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")*";
        private const string UpdateString = UpdatePhrase + @"(" + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")(\." + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")*";
        private const string DeleteString = DeletePhrase + "(" + FromPhrase + @")?(" + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")(\." + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")*";
        private const string CreateString = CreatePhrase + ObjectTypePhrase;
        private const string DropString = DropPhrase + ObjectTypePhrase;
        private const string AlterString = AlterPhrase + ObjectTypePhrase + ".*";
        private const string CallString = CallPhrase + CallObjectPhrase;
        private const string SetString = SetPhrase + VariableNamePhrase;
        private const string DeclareString = DeclarePhrase + VariableNamePhrase;

        private static readonly Regex CommentPattern = new Regex(CommentPhrase, RegexOptions.Compiled | RegexOptions.Singleline);
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
        /// <returns>A ParsedDatabaseStaetment if some heuristic matches; otherwise null</returns>
        public static ParsedSqlStatement GetParsedDatabaseStatement(CommandType commandType, string commandText)
        {
            try
            {
                // Remove comments.
                var statement = CommentPattern.Replace(commandText, "").TrimStart();

                var parsedSqlStatement = _statementParser(commandType, commandText, statement);

                return parsedSqlStatement ?? new ParsedSqlStatement(null, null);
            }
            catch
            {
                return new ParsedSqlStatement(null, null);
            }
        }

        public static bool IsValidName(string name)
        {
            return ValidMetricNameMatcher.IsMatch(name);
        }

        public static bool IsSingleSqlStatement(string sql)
        {
            return SingleSqlStatementMatcher.IsMatch(sql);
        }

        private readonly static List<string> _operations = new List<string>();
        public static IEnumerable<string> Operations => _operations;

        static SqlParser()
        {
            // as for example
            //   ... FROM Northwind.dbo.[Order Details] AS OrdDet ...
            // This was first noticed for the SELECT ... FROM statements from the Northwind example DB.

            // Order these statement parsers in descending order of frequency of use.
            // We'll do a linear search through the table to find the appropriate matcher.
            _statementParser = CreateCompoundStatementParser(
                CreateTableDirectAndStoredProcedureStatementParser(),

                // selects are tricky with the set crap on the front of the statement..
                // leave those out of the dictionary
                new DefaultStatementParser("select", SelectRegex).ParseStatement,

                CreateStatementParserDictionary(
                    new ShowStatementParser(),
                    new DefaultStatementParser("insert", InsertRegex),
                    new DefaultStatementParser("update", UpdateRegex),
                    new DefaultStatementParser("delete", DeleteRegex),

                    new DefaultStatementParser("ExecuteProcedure", ExecuteProcedureRegex1, "exec"),
                    new DefaultStatementParser("ExecuteProcedure", ExecuteProcedureRegex2, "execute"),
                    // Invocation of a conventionally named stored procedure.
                    new DefaultStatementParser("ExecuteProcedure", ExecuteProcedureRegex3, "sp_"),

                    new DefaultStatementParser("create", CreateRegex),
                    new DefaultStatementParser("drop", DropRegex),
                    new DefaultStatementParser("alter", AlterRegex),
                    new DefaultStatementParser("call", CallRegex),

                    // See http://msdn.microsoft.com/en-us/library/ms189484.aspx
                    // The set statement targets a local identifier whose name may start with @.  We just scan over the @.
                    new DefaultStatementParser("set", SetRegex),

                    // See http://msdn.microsoft.com/en-us/library/ms188927.aspx
                    // The declare statement targets a local identifier whose name may start with @.  We just scan over the @.
                    new DefaultStatementParser("declare", DeclareRegex)),

                new SelectVariableStatementParser().ParseStatement,

                // The Waitfor statement is [probably] only in Transact SQL.  There are multiple variations of the statement.
                // See http://msdn.microsoft.com/en-us/library/ms187331.aspx
                new WaitforStatementParser().ParseStatement);
        }

        private delegate ParsedSqlStatement ParseStatement(CommandType commandType, string commandText, string statement);

        private static ParseStatement CreateCompoundStatementParser(params ParseStatement[] parsers)
        {
            return (commandType, commandText, statement) =>
            {
                foreach (var parser in parsers)
                {
                    var parsedStatement = parser(commandType, commandText, statement);
                    if (parsedStatement != null) return parsedStatement;
                }
                return null;
            };
        }

        /// <summary>
        /// A direct match to a table, using the featuers in the IDbCommand.
        /// </summary>
        private static ParseStatement CreateTableDirectAndStoredProcedureStatementParser()
        {
            return (commandType, commandText, statement) =>
            {
                switch (commandType)
                {
                    case CommandType.TableDirect:
                        return new ParsedSqlStatement(commandText, "select");
                    case CommandType.StoredProcedure:
                        return new ParsedSqlStatement(StringsHelper.FixDatabaseObjectName(commandText), "ExecuteProcedure");
                }
                return null;
            };
        }

        private static ParseStatement CreateStatementParserDictionary(params DefaultStatementParser[] parsers)
        {
            char[] delimiters = new char[] { ' ', '(', '\r', '\t', '\n' };
            Dictionary<string, ParseStatement> keywordToParser = new Dictionary<string, ParseStatement>();
            foreach (var parser in parsers)
            {
                keywordToParser[parser.Keyword] = parser.ParseStatement;
            }

            return (commandType, commandText, statement) =>
            {
                int splitIndex = statement.Length;
                foreach (char c in delimiters)
                {
                    int index = statement.IndexOf(c, 0, splitIndex);
                    if (index > 0)
                    {
                        splitIndex = index;
                    }
                }

                string keyword = statement.Substring(0, splitIndex).ToLower();
                // hack for one of the stored procedure parsers
                if (keyword.StartsWith("sp_"))
                {
                    keyword = "sp_";
                }

                if (keywordToParser.TryGetValue(keyword, out ParseStatement parser))
                {
                    return parser(commandType, commandText, statement);
                }

                return null;
            };
        }

        class DefaultStatementParser
        {
            private readonly Regex _pattern;
            private readonly string _key;
            public string Keyword { get; }

            public DefaultStatementParser(string key, Regex pattern) :
                this(key, pattern, key)
            { }

            public DefaultStatementParser(string key, Regex pattern, string keyword)
            {
                this._key = key;
                this._pattern = pattern;
                this.Keyword = keyword;
                SqlParser._operations.Add(key);
            }

            public virtual ParsedSqlStatement ParseStatement(CommandType commandType, string commandText, string statement)
            {
                var matcher = _pattern.Match(statement);
                if (!matcher.Success)
                    return null;

                var model = "unknown";
                foreach (var g in matcher.Groups)
                {
                    if (g is Group)
                    {
                        var str = g.ToString();
                        if (!string.IsNullOrEmpty(str)) model = str;
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
                        model = "ParseError";
                }
                return CreateParsedDatabaseStatement(model);
            }

            protected virtual bool IsValidModelName(string name)
            {
                return IsValidName(name);
            }

            protected virtual ParsedSqlStatement CreateParsedDatabaseStatement(string model)
            {
                return new ParsedSqlStatement(model.ToLower(), _key);
            }
        }

        private class SelectVariableStatementParser
        {
            private readonly ParsedSqlStatement _innerSelectStatement = new ParsedSqlStatement("(subquery)", "select");
            private readonly ParsedSqlStatement _variableStatement = new ParsedSqlStatement("VARIABLE", "select");

            private static readonly Regex SelectMatcher = new Regex(@"^select\s+([^\s,]*).*", PatternSwitches);
            private static readonly Regex FromMatcher = new Regex(@"\s+from\s+", PatternSwitches);

            public ParsedSqlStatement ParseStatement(CommandType commandType, string commandText, string statement)
            {
                var matcher = SelectMatcher.Match(statement);
                if (matcher.Success)
                {
                    return FromMatcher.Match(statement).Success ? _innerSelectStatement : _variableStatement;
                }
                return null;
            }

        }

        private class ShowStatementParser : DefaultStatementParser
        {
            public ShowStatementParser() : base("show", new Regex(@"^\s*show\s+(.*)$", PatternSwitches))
            {
            }

            protected override bool IsValidModelName(string name)
            {
                return true;
            }

            protected override ParsedSqlStatement CreateParsedDatabaseStatement(string model)
            {
                if (model.Length > 50)
                {
                    model = model.Substring(0, 50);
                }
                return new ParsedSqlStatement(model, "show");
            }
        }

        /// <summary>
        /// The Waitfor statement is [probably] only in Transact SQL.  There are multiple variations of the statement.
        /// See http://msdn.microsoft.com/en-us/library/ms187331.aspx
        /// </summary>
        private class WaitforStatementParser : DefaultStatementParser
        {
            public WaitforStatementParser() : base("waitfor", new Regex(@"^waitfor\s+(delay|time)\s+([^\s,(;]*).*", PatternSwitches))
            {
            }

            // All time stamps we match with the Regex are assumed to be valid "names" for our purpose.
            protected override bool IsValidModelName(string name)
            {
                return true;
            }

            protected override ParsedSqlStatement CreateParsedDatabaseStatement(string model)
            {
                // We drop the time string in this.model on the floor.  It may contain quotes, colons, periods, etc.
                return new ParsedSqlStatement("time", "waitfor");
            }
        }

        /// <summary>
        ///This method takes any parameters in the given command and plugs them back into the parameterized command text.
        /// </summary>
        public static void FixParameterizedSql(IDbCommand command)
        {
            if (command.Parameters.Count == 0)
            {
                return;
            }

            List<IDbDataParameter> dbParams = new List<IDbDataParameter>();
            foreach (IDbDataParameter dbParam in command.Parameters)
            {
                dbParams.Add(dbParam);
            }
            command.Parameters.Clear();

            dbParams.Sort(new ParameterComparer());

            string sql = command.CommandText;
            foreach (object parameter in dbParams)
            {
                IDbDataParameter dbParam = (IDbDataParameter)parameter;
                //DebugParam(dbParam, sqlObfuscator);
                DbType type = dbParam.DbType;
                object value = dbParam.Value;
                if (quotableTypes.Contains(type.ToString()))  // the TypeCode for Strings is Int32 for some reason
                {
                    value = QuoteString(value.ToString());
                }
                else if (value is bool)
                {
                    value = ((bool)value) ? 1 : 0;
                }


                // Parameter names can be supplied with the prefix @ or without
                // if not supplied, add the @ to the beginning of the param name
                var paramName = dbParam.ParameterName;
                if (!paramName.StartsWith(SqlParamPrefix))
                {
                    paramName = SqlParamPrefix + paramName;
                }


                sql = sql.Replace(dbParam.ParameterName, value.ToString());
            }

            command.CommandText = sql;
        }

        /// <summary>
        /// Sort parameters so that longer parameter names are sorted to the top.  We want to make sure
        /// that if one parameter name contains another parameter name, the longer name is replaced first.
        /// For example, if two params @Name and @Namespace are given, we want to replace the @Namespace 
        /// values before the @Name values.
        /// </summary>
        private class ParameterComparer : IComparer<IDbDataParameter>
        {
            public int Compare(IDbDataParameter x, IDbDataParameter y)
            {
                return y.ParameterName.Length - x.ParameterName.Length;
            }
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
        };

        public static readonly ParsedSqlStatement NullStatement = new ParsedSqlStatement(null, null);

        private static string QuoteString(string str)
        {
            //All single quotes in the string are replaced by two singe quotes as sql server doesn't parse single quotes in strings. 
            return "'" + str.Replace("'", "''") + "'";
        }
    }

}
