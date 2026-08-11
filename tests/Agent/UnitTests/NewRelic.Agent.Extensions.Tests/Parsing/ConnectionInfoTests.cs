// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Parsing;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Parsing;

[TestFixture]
public class ConnectionInfoTests
{
    [Test]
    public void WithDatabaseName_PortBasedConnectionInfo_PreservesEverythingButDatabaseName()
    {
        var original = new ConnectionInfo("myhost", 1433, "originalDb", "myInstance");

        var copy = original.WithDatabaseName("switchedDb");

        Assert.Multiple(() =>
        {
            Assert.That(copy.Host, Is.EqualTo("myhost"));
            Assert.That(copy.Port, Is.EqualTo(1433));
            Assert.That(copy.PortPathOrId, Is.EqualTo("1433"));
            Assert.That(copy.InstanceName, Is.EqualTo("myInstance"));
            Assert.That(copy.DatabaseName, Is.EqualTo("switchedDb"));
        });
    }

    [Test]
    public void WithDatabaseName_PathOrIdBasedConnectionInfo_PreservesPathOrIdAndLeavesPortNull()
    {
        var original = new ConnectionInfo("myhost", "/path/to/socket", "originalDb", "myInstance");

        var copy = original.WithDatabaseName("switchedDb");

        Assert.Multiple(() =>
        {
            Assert.That(copy.Host, Is.EqualTo("myhost"));
            Assert.That(copy.Port, Is.Null);
            Assert.That(copy.PathOrId, Is.EqualTo("/path/to/socket"));
            Assert.That(copy.PortPathOrId, Is.EqualTo("/path/to/socket"));
            Assert.That(copy.InstanceName, Is.EqualTo("myInstance"));
            Assert.That(copy.DatabaseName, Is.EqualTo("switchedDb"));
        });
    }

    [Test]
    public void WithDatabaseName_NegativePortIsNotCarriedOver_PortStaysNullAndPathOrIdIsUsed()
    {
        // The port ctor only assigns Port when port >= 0, so a negative port leaves
        // Port null and PathOrId set to "unknown". The copy must reproduce that exactly.
        var original = new ConnectionInfo("myhost", -1, "originalDb");

        var copy = original.WithDatabaseName("switchedDb");

        Assert.Multiple(() =>
        {
            Assert.That(copy.Port, Is.Null);
            Assert.That(copy.PathOrId, Is.EqualTo("unknown"));
            Assert.That(copy.PortPathOrId, Is.EqualTo("unknown"));
            Assert.That(copy.DatabaseName, Is.EqualTo("switchedDb"));
        });
    }

    [Test]
    public void WithDatabaseName_NullInstanceName_StaysNull()
    {
        var original = new ConnectionInfo("myhost", 1433, "originalDb");

        var copy = original.WithDatabaseName("switchedDb");

        Assert.That(copy.InstanceName, Is.Null);
    }

    [Test]
    [TestCase(null)]
    [TestCase("")]
    public void WithDatabaseName_NullOrEmptyDatabaseName_NormalizesToUnknown(string databaseName)
    {
        var original = new ConnectionInfo("myhost", 1433, "originalDb");

        var copy = original.WithDatabaseName(databaseName);

        Assert.That(copy.DatabaseName, Is.EqualTo("unknown"));
    }

    [Test]
    public void WithDatabaseName_DoesNotMutateTheOriginal()
    {
        // ConnectionInfoParser.FromConnectionString can hand back a shared static
        // Empty instance, so mutating the receiver would corrupt process-wide state.
        var original = new ConnectionInfo("myhost", 1433, "originalDb", "myInstance");

        var copy = original.WithDatabaseName("switchedDb");

        Assert.Multiple(() =>
        {
            Assert.That(original.DatabaseName, Is.EqualTo("originalDb"));
            Assert.That(copy, Is.Not.SameAs(original));
        });
    }
}
