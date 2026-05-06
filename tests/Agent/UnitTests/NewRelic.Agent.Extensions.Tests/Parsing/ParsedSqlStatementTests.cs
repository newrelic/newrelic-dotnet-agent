// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace ParsingTests;

[TestFixture]
public class ParsedSqlStatementTests
{
    [Test]
    public void EnumConstructor_SetsDatastoreVendorNameString_ToEnumName()
    {
        var statement = new ParsedSqlStatement(DatastoreVendor.MSSQL, "users", "select");

        Assert.Multiple(() =>
        {
            Assert.That(statement.DatastoreVendor, Is.EqualTo(DatastoreVendor.MSSQL));
            Assert.That(statement.DatastoreVendorNameString, Is.EqualTo("MSSQL"));
            Assert.That(statement.Model, Is.EqualTo("users"));
            Assert.That(statement.Operation, Is.EqualTo("select"));
            Assert.That(statement.DatastoreStatementMetricName, Is.EqualTo("Datastore/statement/MSSQL/users/select"));
        });
    }

    [Test]
    public void EnumConstructor_NullOperation_DefaultsToOther()
    {
        var statement = new ParsedSqlStatement(DatastoreVendor.MySQL, "users", null);

        Assert.That(statement.Operation, Is.EqualTo("other"));
    }

    [Test]
    public void StringConstructor_SetsCustomVendorName()
    {
        var statement = new ParsedSqlStatement("DynamoDB", "users", "select");

        Assert.Multiple(() =>
        {
            Assert.That(statement.DatastoreVendor, Is.EqualTo(DatastoreVendor.Other));
            Assert.That(statement.DatastoreVendorNameString, Is.EqualTo("DynamoDB"));
            Assert.That(statement.Model, Is.EqualTo("users"));
            Assert.That(statement.Operation, Is.EqualTo("select"));
            Assert.That(statement.DatastoreStatementMetricName, Is.EqualTo("Datastore/statement/DynamoDB/users/select"));
        });
    }

    [Test]
    public void StringConstructor_NullOperation_DefaultsToOther()
    {
        var statement = new ParsedSqlStatement("DynamoDB", "users", null);

        Assert.That(statement.Operation, Is.EqualTo("other"));
    }

    [TestCase(null)]
    [TestCase("")]
    public void StringConstructor_NullOrEmptyVendor_FallsBackToOther(string vendor)
    {
        var statement = new ParsedSqlStatement(vendor, "users", "select");

        Assert.Multiple(() =>
        {
            Assert.That(statement.DatastoreVendor, Is.EqualTo(DatastoreVendor.Other));
            Assert.That(statement.DatastoreVendorNameString, Is.EqualTo("Other"));
            Assert.That(statement.DatastoreStatementMetricName, Is.EqualTo("Datastore/statement/Other/users/select"));
        });
    }

    [Test]
    public void StringConstructor_ToString_ReturnsModelSlashOperation()
    {
        var statement = new ParsedSqlStatement("Cassandra", "orders", "insert");

        Assert.That(statement.ToString(), Is.EqualTo("orders/insert"));
    }

    [Test]
    public void FromOperation_Enum_ReturnsStatementWithNullModel()
    {
        var statement = ParsedSqlStatement.FromOperation(DatastoreVendor.MSSQL, "select");

        Assert.Multiple(() =>
        {
            Assert.That(statement.DatastoreVendor, Is.EqualTo(DatastoreVendor.MSSQL));
            Assert.That(statement.DatastoreVendorNameString, Is.EqualTo("MSSQL"));
            Assert.That(statement.Model, Is.Null);
            Assert.That(statement.Operation, Is.EqualTo("select"));
        });
    }

    [Test]
    public void FromOperation_String_ReturnsStatementWithNullModel()
    {
        var statement = ParsedSqlStatement.FromOperation("DynamoDB", "scan");

        Assert.Multiple(() =>
        {
            Assert.That(statement.DatastoreVendor, Is.EqualTo(DatastoreVendor.Other));
            Assert.That(statement.DatastoreVendorNameString, Is.EqualTo("DynamoDB"));
            Assert.That(statement.Model, Is.Null);
            Assert.That(statement.Operation, Is.EqualTo("scan"));
        });
    }
}
