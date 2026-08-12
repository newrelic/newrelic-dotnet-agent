// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Parsing;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Parsing;

[TestFixture]
public class ConnectionInfoResolverTests
{
    [Test]
    public void ResolveWithLiveDatabase_LiveNameMatchesParsedName_ReturnsTheSameInstance()
    {
        var parsed = new ConnectionInfo("myhost", 1433, "originalDb");

        var result = ConnectionInfoResolver.ResolveWithLiveDatabase(parsed, "originalDb");

        Assert.That(result, Is.SameAs(parsed));
    }

    [Test]
    public void ResolveWithLiveDatabase_LiveNameDiffersOnlyByCase_ReturnsTheSameInstance()
    {
        // MSSQL returns connection-string casing when a connection is first opened but
        // server casing after a switch. A case-sensitive compare would read every call as
        // a database switch.
        var parsed = new ConnectionInfo("myhost", 1433, "originalDb");

        var result = ConnectionInfoResolver.ResolveWithLiveDatabase(parsed, "ORIGINALDB");

        Assert.That(result, Is.SameAs(parsed));
    }

    [Test]
    [TestCase(null)]
    [TestCase("")]
    public void ResolveWithLiveDatabase_NoLiveDatabaseName_ReturnsTheSameInstance(string liveDatabaseName)
    {
        // Providers that do not expose a live database name (Oracle, for one) must take
        // the unchanged path.
        var parsed = new ConnectionInfo("myhost", 1521, "originalDb");

        var result = ConnectionInfoResolver.ResolveWithLiveDatabase(parsed, liveDatabaseName);

        Assert.That(result, Is.SameAs(parsed));
    }

    [Test]
    public void ResolveWithLiveDatabase_LiveNameDiffers_ReturnsACopyCarryingTheLiveName()
    {
        var parsed = new ConnectionInfo("myhost", 1433, "originalDb", "myInstance");

        var result = ConnectionInfoResolver.ResolveWithLiveDatabase(parsed, "switchedDb");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.SameAs(parsed));
            Assert.That(result.DatabaseName, Is.EqualTo("switchedDb"));
            Assert.That(result.Host, Is.EqualTo("myhost"));
            Assert.That(result.Port, Is.EqualTo(1433));
            Assert.That(result.InstanceName, Is.EqualTo("myInstance"));
            Assert.That(parsed.DatabaseName, Is.EqualTo("originalDb"), "the parsed instance must not be mutated");
        });
    }

    [Test]
    public void ResolveWithLiveDatabase_RepeatedCallsNamingOneDatabase_ReuseTheSameCopy()
    {
        // The steady state for a connection string that omits the database: the parsed name
        // is "unknown", the live name never matches it, so this path runs on every command
        // of every transaction. It must not build a new ConnectionInfo each time.
        var parsed = new ConnectionInfo("myhost", 1433, null);

        var first = ConnectionInfoResolver.ResolveWithLiveDatabase(parsed, "switchedDb");
        var second = ConnectionInfoResolver.ResolveWithLiveDatabase(parsed, "switchedDb");

        Assert.Multiple(() =>
        {
            Assert.That(parsed.DatabaseName, Is.EqualTo("unknown"), "an absent database name parses to unknown");
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.DatabaseName, Is.EqualTo("switchedDb"));
        });
    }

    [Test]
    public void ResolveWithLiveDatabase_SwitchingToASecondDatabaseAndBack_ReusesTheFirstCopy()
    {
        // The case that proves the cache earns its keep: an application that moves off a
        // database and later returns to it must get the copy built the first time, not a
        // fresh one. It also guards against a stale name being handed back on the way there.
        var parsed = new ConnectionInfo("myhost", 1433, "originalDb");

        var first = ConnectionInfoResolver.ResolveWithLiveDatabase(parsed, "dbOne");
        var second = ConnectionInfoResolver.ResolveWithLiveDatabase(parsed, "dbTwo");
        var backAgain = ConnectionInfoResolver.ResolveWithLiveDatabase(parsed, "dbOne");

        Assert.Multiple(() =>
        {
            Assert.That(first.DatabaseName, Is.EqualTo("dbOne"));
            Assert.That(second.DatabaseName, Is.EqualTo("dbTwo"));
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(backAgain, Is.SameAs(first), "returning to dbOne must hit the cache");
            Assert.That(ConnectionInfoResolver.ResolveWithLiveDatabase(parsed, "dbTwo"), Is.SameAs(second),
                "and dbTwo must still be cached too");
        });
    }

    [Test]
    public void ResolveWithLiveDatabase_CyclingThroughSeveralDatabases_ReusesEveryCopy()
    {
        // A multi-tenant application cycling over a handful of databases on one pooled
        // connection is the workload this cache exists for. A second lap must allocate nothing.
        var parsed = new ConnectionInfo("myhost", 1433, "originalDb");
        var names = new[] { "tenantA", "tenantB", "tenantC", "tenantD" };

        var firstLap = new ConnectionInfo[names.Length];
        for (var i = 0; i < names.Length; i++)
        {
            firstLap[i] = ConnectionInfoResolver.ResolveWithLiveDatabase(parsed, names[i]);
        }

        Assert.Multiple(() =>
        {
            for (var i = 0; i < names.Length; i++)
            {
                Assert.That(firstLap[i].DatabaseName, Is.EqualTo(names[i]));
                Assert.That(ConnectionInfoResolver.ResolveWithLiveDatabase(parsed, names[i]), Is.SameAs(firstLap[i]),
                    $"the second lap must reuse the copy for {names[i]}");
            }
        });
    }

    [Test]
    public void ResolveWithLiveDatabase_DatabaseNameContainingAPipe_IsCarriedThroughVerbatim()
    {
        // The earlier version of this method built a cache key by joining the connection
        // string and the database name with a pipe, which two different pairs could collide
        // on. There is no composite key any more, so a pipe is just another character.
        var parsed = new ConnectionInfo("myhost", 1433, "originalDb");

        var result = ConnectionInfoResolver.ResolveWithLiveDatabase(parsed, "odd|name");

        Assert.Multiple(() =>
        {
            Assert.That(result.DatabaseName, Is.EqualTo("odd|name"));
            Assert.That(ConnectionInfoResolver.ResolveWithLiveDatabase(parsed, "odd|name"), Is.SameAs(result));
        });
    }

    [Test]
    public void ResolveWithLiveDatabase_NullParsedConnectionInfo_ReturnsNullWithoutThrowing()
    {
        // GetOrSetValueFromCache returns null when its key is null, and the key is the
        // connection string, so a provider reporting a null connection string lands here.
        // Returning null unchanged matches the behavior before this change.
        var result = ConnectionInfoResolver.ResolveWithLiveDatabase(null, "switchedDb");

        Assert.That(result, Is.Null);
    }
}
