// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Parsing;
using NUnit.Framework;
using Telerik.JustMock;

namespace Agent.Extensions.Tests.Parsing;

[TestFixture]
public class ConnectionInfoResolverTests
{
    private const string ConnectionString = "Server=myhost;Database=originalDb;";

    private ITransaction _transaction;
    private string _capturedCacheKey;
    private int _cacheCallCount;

    [SetUp]
    public void SetUp()
    {
        _capturedCacheKey = null;
        _cacheCallCount = 0;

        _transaction = Mock.Create<ITransaction>();

        // Simulate a cache miss: record the key and evaluate the factory.
        Mock.Arrange(() => _transaction.GetOrSetValueFromCache(Arg.IsAny<string>(), Arg.IsAny<Func<object>>()))
            .Returns((string key, Func<object> func) =>
            {
                _capturedCacheKey = key;
                _cacheCallCount++;
                return func();
            });
    }

    [Test]
    public void ResolveWithLiveDatabase_LiveNameMatchesParsedName_ReturnsTheSameInstanceAndDoesNotTouchTheCache()
    {
        var parsed = new ConnectionInfo("myhost", 1433, "originalDb");

        var result = ConnectionInfoResolver.ResolveWithLiveDatabase(_transaction, parsed, ConnectionString, "originalDb");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.SameAs(parsed));
            Assert.That(_cacheCallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void ResolveWithLiveDatabase_LiveNameDiffersOnlyByCase_ReturnsTheSameInstanceAndDoesNotTouchTheCache()
    {
        // MSSQL returns connection-string casing when a connection is first opened but
        // server casing after a switch. A case-sensitive compare would fire the guard on
        // every single call and pollute the per-transaction cache.
        var parsed = new ConnectionInfo("myhost", 1433, "originalDb");

        var result = ConnectionInfoResolver.ResolveWithLiveDatabase(_transaction, parsed, ConnectionString, "ORIGINALDB");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.SameAs(parsed));
            Assert.That(_cacheCallCount, Is.EqualTo(0));
        });
    }

    [Test]
    [TestCase(null)]
    [TestCase("")]
    public void ResolveWithLiveDatabase_NoLiveDatabaseName_ReturnsTheSameInstanceAndDoesNotTouchTheCache(string liveDatabaseName)
    {
        // Providers that do not expose a live database name (Oracle, for one) must take
        // the unchanged path.
        var parsed = new ConnectionInfo("myhost", 1521, "originalDb");

        var result = ConnectionInfoResolver.ResolveWithLiveDatabase(_transaction, parsed, ConnectionString, liveDatabaseName);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.SameAs(parsed));
            Assert.That(_cacheCallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void ResolveWithLiveDatabase_LiveNameDiffers_ReturnsACopyCarryingTheLiveName()
    {
        var parsed = new ConnectionInfo("myhost", 1433, "originalDb", "myInstance");

        var result = ConnectionInfoResolver.ResolveWithLiveDatabase(_transaction, parsed, ConnectionString, "switchedDb");

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
    public void ResolveWithLiveDatabase_LiveNameDiffers_CachesUnderACompositeKey()
    {
        // The parsed ConnectionInfo is already cached under the bare connection string,
        // so the switched-database copy needs a distinct key or it would overwrite it.
        var parsed = new ConnectionInfo("myhost", 1433, "originalDb");

        ConnectionInfoResolver.ResolveWithLiveDatabase(_transaction, parsed, ConnectionString, "switchedDb");

        Assert.Multiple(() =>
        {
            Assert.That(_cacheCallCount, Is.EqualTo(1));
            Assert.That(_capturedCacheKey, Is.EqualTo(ConnectionString + "|switchedDb"));
        });
    }

    [Test]
    public void ResolveWithLiveDatabase_CacheHit_ReturnsTheCachedInstanceWithoutBuildingANewOne()
    {
        var parsed = new ConnectionInfo("myhost", 1433, "originalDb");
        var cached = parsed.WithDatabaseName("switchedDb");

        // Simulate a cache hit: return the stored value and never evaluate the factory.
        Mock.Arrange(() => _transaction.GetOrSetValueFromCache(Arg.IsAny<string>(), Arg.IsAny<Func<object>>()))
            .Returns(cached);

        var result = ConnectionInfoResolver.ResolveWithLiveDatabase(_transaction, parsed, ConnectionString, "switchedDb");

        Assert.That(result, Is.SameAs(cached));
    }

    [Test]
    public void ResolveWithLiveDatabase_NullParsedConnectionInfo_ReturnsNullWithoutThrowing()
    {
        // GetOrSetValueFromCache returns null when its key is null, and the key is the
        // connection string, so a provider reporting a null connection string lands here.
        // Returning null unchanged matches the behavior before this change.
        var result = ConnectionInfoResolver.ResolveWithLiveDatabase(_transaction, null, ConnectionString, "switchedDb");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            Assert.That(_cacheCallCount, Is.EqualTo(0));
        });
    }
}
