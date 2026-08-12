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

    [Test]
    public void WithDatabaseName_CalledTwiceWithTheSameName_ReturnsTheMemoizedCopy()
    {
        // This runs on every SQL command for a connection whose string omits the database,
        // so the second call must not build a second copy.
        var original = new ConnectionInfo("myhost", 1433, "originalDb");

        var first = original.WithDatabaseName("switchedDb");
        var second = original.WithDatabaseName("switchedDb");

        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public void WithDatabaseName_CalledAgainWithDifferentCasing_ReturnsTheMemoizedCopy()
    {
        var original = new ConnectionInfo("myhost", 1433, "originalDb");

        var first = original.WithDatabaseName("switchedDb");
        var second = original.WithDatabaseName("SWITCHEDDB");

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.DatabaseName, Is.EqualTo("switchedDb"), "the spelling of the first call wins on a memo hit");
        });
    }

    [Test]
    public void WithDatabaseName_CalledWithADifferentName_BuildsAFreshCopyAndKeepsBoth()
    {
        var original = new ConnectionInfo("myhost", 1433, "originalDb");

        var first = original.WithDatabaseName("dbOne");
        var second = original.WithDatabaseName("dbTwo");

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(first.DatabaseName, Is.EqualTo("dbOne"), "the earlier copy keeps its own name");
            Assert.That(second.DatabaseName, Is.EqualTo("dbTwo"));
            Assert.That(original.WithDatabaseName("dbOne"), Is.SameAs(first), "both names stay cached");
            Assert.That(original.WithDatabaseName("dbTwo"), Is.SameAs(second));
        });
    }

    [Test]
    public void WithDatabaseName_NullOrEmptyName_IsNotCached()
    {
        // An absent name carries no information worth a cache entry, and it must not collide
        // with a real database that happens to be reported as "unknown".
        var original = new ConnectionInfo("myhost", 1433, "originalDb");

        var first = original.WithDatabaseName(null);
        var second = original.WithDatabaseName(null);

        Assert.Multiple(() =>
        {
            Assert.That(first.DatabaseName, Is.EqualTo("unknown"));
            Assert.That(second.DatabaseName, Is.EqualTo("unknown"));
            Assert.That(second, Is.Not.SameAs(first));
        });
    }

    [Test]
    public void WithDatabaseName_PastTheCacheCeiling_StillReturnsCorrectCopies()
    {
        // The cache is bounded so an application reaching very many databases through one
        // connection string cannot grow it without limit. Past the ceiling the copies must
        // still be correct; they are simply no longer reused.
        var original = new ConnectionInfo("myhost", 1433, "originalDb", "myInstance");

        // The ceiling is 100, so 150 distinct names is comfortably past it.
        for (var i = 0; i < 150; i++)
        {
            var copy = original.WithDatabaseName("db" + i);
            Assert.That(copy.DatabaseName, Is.EqualTo("db" + i));
            Assert.That(copy.Host, Is.EqualTo("myhost"));
            Assert.That(copy.InstanceName, Is.EqualTo("myInstance"));
        }

        var cachedEarly = original.WithDatabaseName("db0");
        var cachedEarlyAgain = original.WithDatabaseName("db0");
        var pastCeiling = original.WithDatabaseName("db149");
        var pastCeilingAgain = original.WithDatabaseName("db149");

        Assert.Multiple(() =>
        {
            Assert.That(cachedEarlyAgain, Is.SameAs(cachedEarly), "names cached before the ceiling stay cached");
            Assert.That(pastCeilingAgain, Is.Not.SameAs(pastCeiling), "names arriving past the ceiling are built fresh each time");
            Assert.That(pastCeilingAgain.DatabaseName, Is.EqualTo("db149"), "and are still correct");
            Assert.That(original.DatabaseName, Is.EqualTo("originalDb"), "the receiver is untouched throughout");
        });
    }

    [Test]
    public void WithDatabaseName_SharedReceiver_HandsEachCallerACopyMatchingTheNameItAskedFor()
    {
        // The memo lives on the receiver, which ConnectionInfoParser shares across callers.
        // That is safe only because every field of the copy comes from the receiver, so a
        // memo hit is correct for anyone holding it. This pins that down.
        var shared = new ConnectionInfo("myhost", 1433, "originalDb", "myInstance");

        var callerOne = shared.WithDatabaseName("dbOne");
        var callerTwo = shared.WithDatabaseName("dbTwo");

        Assert.Multiple(() =>
        {
            Assert.That(callerOne.DatabaseName, Is.EqualTo("dbOne"));
            Assert.That(callerTwo.DatabaseName, Is.EqualTo("dbTwo"));
            Assert.That(callerTwo.Host, Is.EqualTo("myhost"));
            Assert.That(callerTwo.Port, Is.EqualTo(1433));
            Assert.That(callerTwo.InstanceName, Is.EqualTo("myInstance"));
        });
    }
}
