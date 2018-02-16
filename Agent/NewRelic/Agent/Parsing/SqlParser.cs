using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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

		// Regex Phrases
		private const String SelectPhrase = @"(?=^/\*.*\*/*\s\bset\b.*;\s*\bselect\b|^/\*.*\*/*\s\bselect\b|^\bset\b.*;\s*\bselect\b|^\bselect\b).*?\s+";
		private const String InsertPhrase = @"^insert\s+into\s+";
		private const String UpdatePhrase = @"^update\s+";
		private const String DeletePhrase = @"^delete\s+";
		private const String CreatePhrase = @"^create\s+";
		private const String DropPhrase = @"^drop\s+";
		private const String AlterPhrase = @"^alter\s+";
		private const String CallPhrase = @"^call\s+";
		private const String SetPhrase = @"^set\s+@?";
		private const String DeclarePhrase = @"^declare\s+@?";

		private const String CommentPhrase = @"/\*.*?\*/";
		private const String StartObjectNameSeparator = @"[\s\(\[`\""]*";
		private const String EndObjectNameSeparator = @"[\s\)\]`\""]*";
		// TODO: cp - This doesn't catch spaces inside of object names, even if the names are surrounded by separators. [Table Name] would resolve to simply "Table".
		private const String ValidObjectName = @"([^,;\[\s\]\(\)`\""\.]*)";
		private const String FromPhrase = @"from\s+";
		private const String VariableNamePhrase = @"([^\s(=,]*).*";
		private const String ObjectTypePhrase = @"([^\s]*)";
		private const String CallObjectPhrase = @"([^\s(,]*).*";
		private const String MetricNamePhrase = @"^[a-z0-9.\$_]*$";

		// Regex Strings
		private const String SelectString = SelectPhrase + FromPhrase + @"(" + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")(\." + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")*";
		private const String InsertString = InsertPhrase + @"(" + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")(\." + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")*";
		private const String UpdateString = UpdatePhrase + @"(" + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")(\." + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")*";
		private const String DeleteString = DeletePhrase + "(" + FromPhrase + @")?(" + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")(\." + StartObjectNameSeparator + ValidObjectName + EndObjectNameSeparator + @")*";
		private const String CreateString = CreatePhrase + ObjectTypePhrase;
		private const String DropString = DropPhrase + ObjectTypePhrase;
		private const String AlterString = AlterPhrase + ObjectTypePhrase + ".*";
		private const String CallString = CallPhrase + CallObjectPhrase;
		private const String SetString = SetPhrase + VariableNamePhrase;
		private const String DeclareString = DeclarePhrase + VariableNamePhrase;

		private static readonly Regex CommentPattern = new Regex(CommentPhrase, RegexOptions.Compiled | RegexOptions.Singleline);
		private static readonly Regex ValidMetricNameMatcher = new Regex(MetricNamePhrase, RegexOptions.Compiled | RegexOptions.IgnoreCase);
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
		public static ParsedSqlStatement GetParsedDatabaseStatement(DatastoreVendor datastoreVendor, CommandType commandType, String commandText)
		{			 
			try {
				switch (commandType)
				{
					case CommandType.TableDirect:
						return new ParsedSqlStatement(datastoreVendor, commandText, "select");
					case CommandType.StoredProcedure:
						return new ParsedSqlStatement(datastoreVendor, StringsHelper.FixDatabaseObjectName(commandText), "ExecuteProcedure");
				}
				// Remove comments.
				var statement = CommentPattern.Replace(commandText, "").TrimStart();

				if (statement.IndexOf(' ') < 0)
				{
					return new ParsedSqlStatement(datastoreVendor, StringsHelper.FixDatabaseObjectName(statement), "ExecuteProcedure");
				}

				return _statementParser(datastoreVendor, commandType, commandText, statement);
			} 
			catch
			{
				return new ParsedSqlStatement(datastoreVendor, null, null);
			}
		}
		
		public static Boolean IsValidName(String name)
		{
			return ValidMetricNameMatcher.IsMatch(name);
		}

		private readonly static HashSet<string> _operations = new HashSet<string>();
		public static IEnumerable<string> Operations => _operations;
		
		static SqlParser()
		{
			// as for example
			//   ... FROM Northwind.dbo.[Order Details] AS OrdDet ...
			// This was first noticed for the SELECT ... FROM statements from the Northwind example DB.

			// Order these statement parsers in descending order of frequency of use.
			// We'll do a linear search through the table to find the appropriate matcher.
			_statementParser = CreateCompoundStatementParser(

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

		private delegate ParsedSqlStatement ParseStatement(DatastoreVendor datastoreVendor, CommandType commandType, String commandText, String statement);

		private static ParseStatement CreateCompoundStatementParser(params ParseStatement[] parsers)
		{
			return (datastoreVendor, commandType, commandText, statement) =>
			{
				foreach (var parser in parsers)
				{
					var parsedStatement = parser(datastoreVendor, commandType, commandText, statement);
					if (parsedStatement != null) return parsedStatement;
				}
				return new ParsedSqlStatement(datastoreVendor, null, null);
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

			return (datastoreVendor, commandType, commandText, statement) =>
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
					return parser(datastoreVendor, commandType, commandText, statement);
				}

				return null;
			};
		}

		class DefaultStatementParser
		{
		    private readonly Regex _pattern;
		    private readonly string _key;
			public string Keyword { get; }

			public DefaultStatementParser(String key, Regex pattern) :
				this(key, pattern, key)
			{ }

			public DefaultStatementParser(String key, Regex pattern, string keyword)
			{
				this._key = key;
				this._pattern = pattern;
				this.Keyword = keyword;
				SqlParser._operations.Add(key);
			}

			public virtual ParsedSqlStatement ParseStatement(DatastoreVendor vendor, CommandType commandType, String commandText, String statement)
			{
				var matcher = _pattern.Match(statement);
				if (!matcher.Success) 
					return null;

				var model = "unknown";
				foreach (var g in matcher.Groups)
				{
					if (g is Group) {
						var str = g.ToString();
						if (!String.IsNullOrEmpty(str)) model = str;
					}
				}

				if (String.Equals(model, "select", StringComparison.CurrentCultureIgnoreCase))
				{
					model = "(subquery)";
				}
				else
				{
					model = StringsHelper.FixDatabaseObjectName(model);
					if (!IsValidModelName(model))
						model = "ParseError"; 
				}
				return CreateParsedDatabaseStatement(vendor, model);
			}
			
			protected virtual Boolean IsValidModelName(String name) {
				return IsValidName(name);
			}
			
			protected virtual ParsedSqlStatement CreateParsedDatabaseStatement(DatastoreVendor vendor, String model)
			{
				return new ParsedSqlStatement(vendor, model.ToLower(), _key);
			}
		}
		
		private class SelectVariableStatementParser
		{
			private static readonly Regex SelectMatcher = new Regex(@"^select\s+([^\s,]*).*", PatternSwitches);
			private static readonly Regex FromMatcher   = new Regex(@"\s+from\s+", PatternSwitches);

			public ParsedSqlStatement ParseStatement(DatastoreVendor vendor, CommandType commandType, String commandText, String statement)
			{
				var matcher = SelectMatcher.Match(statement);
				if (matcher.Success)
				{
					return FromMatcher.Match(statement).Success ? new ParsedSqlStatement(vendor, "(subquery)", "select") : new ParsedSqlStatement(vendor, "VARIABLE", "select");
				}
				return null;
			}
			
		}
		
		private class ShowStatementParser : DefaultStatementParser {
			public ShowStatementParser() : base("show", new Regex(@"^\s*show\s+(.*)$", PatternSwitches)) {
			}
			
			protected override Boolean IsValidModelName(String name) {
				return true;
			}
			
			protected override ParsedSqlStatement CreateParsedDatabaseStatement(DatastoreVendor vendor, String model)
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
		private class WaitforStatementParser : DefaultStatementParser {
			public WaitforStatementParser() : base("waitfor", new Regex(@"^waitfor\s+(delay|time)\s+([^\s,(;]*).*", PatternSwitches)) {
			}

			// All time stamps we match with the Regex are assumed to be valid "names" for our purpose.
			protected override bool IsValidModelName(String name) {
				return true;
			}

			protected override ParsedSqlStatement CreateParsedDatabaseStatement(DatastoreVendor vendor, String model)
			{
				// We drop the time string in this.model on the floor.  It may contain quotes, colons, periods, etc.
				return new ParsedSqlStatement(vendor, "time", "waitfor");
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

			String sql = command.CommandText;
			foreach (Object parameter in dbParams)
			{
				IDbDataParameter dbParam = (IDbDataParameter)parameter;
				//DebugParam(dbParam, sqlObfuscator);
				DbType type = dbParam.DbType;
				Object value = dbParam.Value;
				if (quotableTypes.Contains(type.ToString()))  // the TypeCode for Strings is Int32 for some reason
				{
					value = QuoteString(value.ToString());
				}
				else if (value is bool)
				{
					value = ((bool)value) ? 1 : 0;
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

		private static readonly List<String> quotableTypes = new List<string>() {
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

		private static String QuoteString(String str)
		{
			//All single quotes in the string are replaced by two singe quotes as sql server doesn't parse single quotes in strings. 
			return "'" + str.Replace("'", "''") + "'";
		}
	}

}
