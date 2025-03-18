// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET9_0

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Couchbase;
using Couchbase.Core.Exceptions;
using Couchbase.KeyValue;
using Couchbase.KeyValue.RangeScan;
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

    private async Task<(ICluster, IBucket)> InitializeAsync()
    {
        // Initialize the Couchbase cluster
        var clusterOptions = new ClusterOptions
        {
            ConnectionString = CouchbaseTestObject.CouchbaseServerUrl,
            UserName = CouchbaseTestObject.Username,
            Password = CouchbaseTestObject.Password
        };

        var cluster = await Cluster.ConnectAsync(clusterOptions);
        await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(10));

        var bucket = await cluster.BucketAsync(CouchbaseTestObject.CouchbaseTestBucket);

        return (cluster, bucket);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task Get()
    {
        var (cluster, bucket) = await InitializeAsync();

        // get a user-defined collection reference
        var scope = await bucket.ScopeAsync("tenant_agent_00");
        var collection = await scope.CollectionAsync("users");

        // get a document
        using var getResult1 = await collection.GetAsync("0");
        using var getResult2 = await collection.GetAnyReplicaAsync("0");
        var tasks = collection.GetAllReplicasAsync("0").ToList();
        await Task.WhenAll(tasks);
        foreach (var t in tasks)
        {
            var result = await t;
            result.Dispose();
        }
        using var getResult3 = await collection.GetAndTouchAsync("0", TimeSpan.FromSeconds(10));
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task GetAndLockAndUnlock()
    {
        var (cluster, bucket) = await InitializeAsync();
        // get a user-defined collection reference
        var scope = await bucket.ScopeAsync("tenant_agent_00");
        var collection = await scope.CollectionAsync("users");

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
        var (cluster, bucket) = await InitializeAsync();
        // get a user-defined collection reference
        var scope = await bucket.ScopeAsync("tenant_agent_00");
        var collection = await scope.CollectionAsync("users");
        // check if a document exists
        await collection.ExistsAsync("0");
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task InsertUpsertReplaceAndRemove()
    {
        var (cluster, bucket) = await InitializeAsync();
        // get a user-defined collection reference
        var scope = await bucket.ScopeAsync("tenant_agent_00");
        var collection = await scope.CollectionAsync("users");

        var key = "my-document-key";

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
        var (cluster, bucket) = await InitializeAsync();
        var inventoryScope = await bucket.ScopeAsync("inventory");
        var hotelCollection = await inventoryScope.CollectionAsync("hotel");
        var mutateInResult = await hotelCollection.MutateInAsync("hotel_10025",
            specs => specs.Upsert("pets_ok", true)
        );
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task Lookup()
    {
        var (cluster, bucket) = await InitializeAsync();
        // get a user-defined collection reference
        var scope = await bucket.ScopeAsync("tenant_agent_00");
        var collection = await scope.CollectionAsync("users");

        // lookup a document
        await collection.LookupInAsync("0", [LookupInSpec.Get("credit_cards")]);
        await collection.LookupInAnyReplicaAsync("0", [LookupInSpec.Get("credit_cards")]);
        var results = collection.LookupInAllReplicasAsync("0", [LookupInSpec.Get("credit_cards")]);
        await foreach (var result in results)
        {
            // process the result
        }
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task Scan()
    {
        var (cluster, bucket) = await InitializeAsync();
        // get a user-defined collection reference
        var scope = await bucket.ScopeAsync("tenant_agent_00");
        var collection = await scope.CollectionAsync("users");
        // scan the collection
        var results = collection.ScanAsync(new RangeScan());
        await foreach (var result in results)
        {
            // process the result
        }
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task Touch()
    {
        var (cluster, bucket) = await InitializeAsync();
        // get a user-defined collection reference
        var scope = await bucket.ScopeAsync("tenant_agent_00");
        var collection = await scope.CollectionAsync("users");
        // update the expiry of a document
        await collection.TouchAsync("0", TimeSpan.FromSeconds(10));
        await collection.TouchWithCasAsync("0", TimeSpan.FromSeconds(10));
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task ScopeQuery()
    {
        var (cluster, bucket) = await InitializeAsync();

        var inventoryScope = await bucket.ScopeAsync("inventory");
        var queryResult = await inventoryScope.QueryAsync<dynamic>("SELECT * FROM airline WHERE id = 10");

    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task ClusterQuery()
    {
        var (cluster, bucket) = await InitializeAsync();

        try
        {
            var options = new CouchbaseManagement.Query.CreatePrimaryQueryIndexOptions();
            options.IgnoreIfExists(true);
            await cluster.QueryIndexes.CreatePrimaryIndexAsync("`travel-sample`", options);
        }
        catch (IndexExistsException) // ignore
        {
        }

        // requires a primary index
        var clusterQueryResult = await cluster.QueryAsync<dynamic>(
            "SELECT t.* FROM `travel-sample` t WHERE t.type=$type",
            options => options.Parameter("type", "landmark")
        );
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task ScopeSearch()
    {
        var (cluster, bucket) = await InitializeAsync();

        var inventoryScope = await bucket.ScopeAsync("inventory");
        var searchResult = await inventoryScope.SearchAsync("index-hotel-description", SearchRequest.Create(new MatchQuery("swanky")), new SearchOptions().Limit(2));
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task ClusterSearch()
    {
        var (cluster, bucket) = await InitializeAsync();

        var clusterSearchResult = await cluster.SearchAsync("hotels", SearchRequest.Create(new MatchQuery("swanky")), new SearchOptions().Limit(2));
    }

}
#endif
