// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Extensions.Parsing;

public class ParsedSqlStatement
{
    private readonly string _asString;

    public DatastoreVendor DatastoreVendor { get; }

    /// <summary>
    /// The vendor name as a string. For enum-based constructors this is the enum name;
    /// for string-based constructors this is the caller-supplied value.
    /// </summary>
    public string DatastoreVendorNameString { get; }

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
    /// <param name="datastoreVendor"></param>
    /// <param name="model">What the statement is operating on, eg the "direct object" of the operation.</param>
    /// <param name="operation">What the operation is doing.</param>
    public ParsedSqlStatement(DatastoreVendor datastoreVendor, string model, string operation)
        : this(datastoreVendor, EnumNameCache<DatastoreVendor>.GetName(datastoreVendor), model, operation)
    {
    }

    /// <summary>
    /// Construct a summarized SQL statement using a string vendor name.
    /// Use this overload when the vendor is not represented by the DatastoreVendor enum.
    /// The DatastoreVendor property will be set to DatastoreVendor.Other.
    /// </summary>
    /// <param name="vendor">The vendor name to use in metric names.</param>
    /// <param name="model">What the statement is operating on, eg the "direct object" of the operation.</param>
    /// <param name="operation">What the operation is doing.</param>
    public ParsedSqlStatement(string vendor, string model, string operation)
        : this(DatastoreVendor.Other, string.IsNullOrEmpty(vendor) ? EnumNameCache<DatastoreVendor>.GetName(DatastoreVendor.Other) : vendor, model, operation)
    {
    }

    private ParsedSqlStatement(DatastoreVendor datastoreVendor, string vendorNameString, string model, string operation)
    {
        Model = model;
        Operation = operation ?? "other";
        DatastoreVendor = datastoreVendor;
        DatastoreVendorNameString = vendorNameString;
        _asString = $"{Model}/{Operation}";
        DatastoreStatementMetricName = $"Datastore/statement/{DatastoreVendorNameString}/{_asString}";
    }

    public static ParsedSqlStatement FromOperation(DatastoreVendor vendor, string operation)
    {
        return new ParsedSqlStatement(vendor, null, operation);
    }

    public static ParsedSqlStatement FromOperation(string vendor, string operation)
    {
        return new ParsedSqlStatement(vendor, null, operation);
    }

    public override string ToString()
    {
        return _asString;
    }
}
