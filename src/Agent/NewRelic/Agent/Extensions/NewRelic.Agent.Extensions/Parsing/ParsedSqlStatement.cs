// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Extensions.Parsing
{
    public class ParsedSqlStatement
    {
        private readonly string _asString;

        public DatastoreVendor DatastoreVendor { get; }

        /// <summary>
        /// The "direct object", eg what the operation is operating on.
        /// </summary>
        public string Model { get; }

        /// <summary>
        /// The operation the data base is performing.
        /// </summary>
        public string Operation { get; }

        public string DatastoreStatementMetricName { get; }

        /// <summary>
        /// Construct a summarized SQL statement.
        /// 
        /// Examples:
        ///   select * from dude ==> ParsedDatabaseStatement("dude", "select");
        ///   set @foo=17 ==> ParsedDatabaseStatement("foo", "set")
        ///  
        /// See DatabaseStatementParserTest for additional examples.
        /// 
        /// </summary>
        /// <param name="model">What the statement is operating on, eg the "direct object" of the operation.</param>
        /// <param name="operation">What the operation is doing.</param>
        public ParsedSqlStatement(DatastoreVendor datastoreVendor, string model, string operation)
        {
            Model = model;
            Operation = operation ?? "other";
            DatastoreVendor = datastoreVendor;
            _asString = $"{Model}/{Operation}";
            DatastoreStatementMetricName = $"Datastore/statement/{EnumNameCache<DatastoreVendor>.GetName(datastoreVendor)}/{_asString}";
        }

        public static ParsedSqlStatement FromOperation(DatastoreVendor vendor, string operation)
        {
            return new ParsedSqlStatement(vendor, null, operation);
        }

        public override string ToString()
        {
            return _asString;
        }

        public ParsedSqlStatement CloneWithNewModel(string model)
        {
            return new ParsedSqlStatement(DatastoreVendor, model, Operation);
        }
    }
}
