// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Couchbase;
using Couchbase.Core.Exceptions;
using Couchbase.KeyValue;

#if NET481_OR_GREATER || NET
using Couchbase.KeyValue.RangeScan;
#endif

using Couchbase.Search;
using Couchbase.Search.Queries.Simple;
using NewRelic.Agent.IntegrationTests.Shared.Couchbase;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

using CouchbaseManagement = Couchbase.Management;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Couchbase;

[Library]
class Couchbase3Exerciser
{
    // instrumented but untested:
    // - AppendAsync
    // - PrependAsync
    // - IncrementAsync
    // - DecrementAsync

    private async Task<(ICluster Cluster, IBucket Bucket)> InitializeAsync()
    {
        // Initialize the Couchbase cluster
        var clusterOptions = new ClusterOptions
        {
            ConnectionString = CouchbaseTestObject.CouchbaseServerUrl,
            UserName = CouchbaseTestObject.Username,
            Password = CouchbaseTestObject.Password
        };

        var cluster = await Cluster.ConnectAsync(clusterOptions);
        var bucket = await cluster.BucketAsync(CouchbaseTestObject.CouchbaseTestBucket);

        return (cluster, bucket);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task Get()
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;

        // get a user-defined collection reference
        var collection = await GetCollectionAsync(bucket, "tenant_agent_00", "users");

        // get a document
        using var getResult1 = await collection.GetAsync("0");
        using var getResult2 = await collection.GetAnyReplicaAsync("0");
        // for some reason, this fails if one of the previous 2 methods isn't called first.
        var result = await Task.WhenAll(collection.GetAllReplicasAsync("0"));
        foreach (var r in result)
            r.Dispose();
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task GetAndLockAndUnlock()
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;

        // get a user-defined collection reference
        var collection = await GetCollectionAsync(bucket, "tenant_agent_00", "users");

        // get a document and lock it
        using var result = await collection.GetAndLockAsync("0", TimeSpan.FromSeconds(10));

        // unlock the document
        await collection.UnlockAsync("0", result.Cas);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task Exists()
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;

        // get a user-defined collection reference
        var collection = await GetCollectionAsync(bucket, "tenant_agent_00", "users");
        // check if a document exists
        await collection.ExistsAsync("0");
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task InsertUpsertReplaceAndRemove()
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;

        // get a user-defined collection reference
        var collection = await GetCollectionAsync(bucket, "tenant_agent_00", "users");

        var key = Guid.NewGuid().ToString();

        // insert a document
        await collection.InsertAsync(key, new { Name = "Ted", Age = 31 });

        // upsert a document
        await collection.UpsertAsync(key, new { Name = "Ted", Age = 32 });

        // replace a document
        await collection.ReplaceAsync(key, new { Name = "Bill", Age = 33 });

        // delete the document
        await collection.RemoveAsync(key);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task Mutate()
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;

        var hotelCollection = await GetCollectionAsync(bucket, "inventory", "hotel");
#if NET481_OR_GREATER || NET
        using var mutateInResult = await hotelCollection.MutateInAsync("hotel_10025",
            specs => specs.Upsert("pets_ok", true)
        );
#else
        await hotelCollection.MutateInAsync("hotel_10025",
            specs => specs.Upsert("pets_ok", true)
        );
#endif
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task Lookup()
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;

        // get a user-defined collection reference
        var collection = await GetCollectionAsync(bucket, "tenant_agent_00", "users");

        // lookup a document
#if NET481_OR_GREATER || NET
        using var result1 = await collection.LookupInAsync("0", [LookupInSpec.Get("credit_cards")]);
        using var result2 = await collection.LookupInAnyReplicaAsync("0", [LookupInSpec.Get("credit_cards")]);
        var results = collection.LookupInAllReplicasAsync("0", [LookupInSpec.Get("credit_cards")]);
        await foreach (var result in results)
        {
            result.Dispose();
        }
#else
        await collection.LookupInAsync("0", [LookupInSpec.Get("credit_cards")]);
#endif
    }

#if NET481_OR_GREATER || NET
    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task Scan()
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;

        // get a user-defined collection reference
        var collection = await GetCollectionAsync(bucket, "tenant_agent_00", "users");

        // scan the collection
        var results = collection.ScanAsync(new RangeScan());
        await foreach (var result in results)
        {
            // process the result
        }
    }
#endif

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task Touch()
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;

        // get a user-defined collection reference
        var collection = await GetCollectionAsync(bucket, "tenant_agent_00", "users");

        // insert a new document so we can touch it and let it expire
        var key = Guid.NewGuid().ToString();
        await collection.InsertAsync(key, new { Name = "Ted", Age = 32 });

        // update the expiry of a document
        using var getResult3 = await collection.GetAndTouchAsync(key, TimeSpan.FromSeconds(10));
        await collection.TouchAsync(key, TimeSpan.FromSeconds(5));
#if NET481_OR_GREATER || NET
        await collection.TouchWithCasAsync(key, TimeSpan.FromSeconds(2));
#endif
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task ScopeQuery()
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;
        var inventoryScope = await bucket.ScopeAsync("inventory");

        using var queryResult = await inventoryScope.QueryAsync<dynamic>("SELECT * FROM airline WHERE id = 10");

    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task ClusterQuery()
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;

        try
        {
            var options = new CouchbaseManagement.Query.CreatePrimaryQueryIndexOptions();
            options.IgnoreIfExists(true);
            await cluster.QueryIndexes.CreatePrimaryIndexAsync("`travel-sample`", options);
        }
        catch (IndexExistsException) // ignore, it might already exist
        {
        }

        // requires a primary index
        using var clusterQueryResult = await cluster.QueryAsync<dynamic>(
            "SELECT t.* FROM `travel-sample` t WHERE t.type=$type",
            options => options.Parameter("type", "landmark")
        );
    }

#if NET481_OR_GREATER || NET
    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task ScopeSearch()
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;

        var inventoryScope = await bucket.ScopeAsync("inventory");

        var searchResult = await inventoryScope.SearchAsync("index-hotel-description", SearchRequest.Create(new MatchQuery("swanky")), new SearchOptions().Limit(2));
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task ClusterSearch()
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;

        var clusterSearchResult = await cluster.SearchAsync("hotels", SearchRequest.Create(new MatchQuery("swanky")), new SearchOptions().Limit(2));
        var clusterSearchQueryResult = await cluster.SearchQueryAsync("hotels", new MatchQuery("swanky"), new SearchOptions().Limit(2));
    }
#endif

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task ClusterAnalytics()
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;

        var result = await cluster.AnalyticsQueryAsync<dynamic>("SELECT VALUE ap FROM `travel-sample`.inventory.airport_view ap limit 1;");
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task ScopeAnalytics()
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;
        var inventoryScope = await bucket.ScopeAsync("inventory");

        var result = await inventoryScope.AnalyticsQueryAsync<dynamic>("SELECT VALUE ap FROM airport_view ap limit 1;");
    }

    private async Task<ICouchbaseCollection> GetCollectionAsync(IBucket bucket, string scopeName, string collectionName)
    {
        var scope = await bucket.ScopeAsync(scopeName);
        var collection = await scope.CollectionAsync(collectionName);

        return await Task.FromResult(collection);
    }

}
