// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
#if !NET462 || NET
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
using LibGit2Sharp;
using Microsoft.Identity.Client;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using Newtonsoft.Json;
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
            ConnectionString = CouchbaseConfiguration.CouchbaseServerUrl,
            UserName = CouchbaseConfiguration.Username,
            Password = CouchbaseConfiguration.Password
        };

        var cluster = await Cluster.ConnectAsync(clusterOptions);
        var bucket = await cluster.BucketAsync(CouchbaseConfiguration.CouchbaseTestBucket);

        return (cluster, bucket);
    }

    [LibraryMethod]
    public async Task InsertTestDocument(string scopeName, string collectionName, string documentId, string base64EncodedSerializedDocument)
    {
        var serializedDocument = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64EncodedSerializedDocument));
        var document = Newtonsoft.Json.JsonConvert.DeserializeObject(serializedDocument);

        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;

        // get a user-defined collection reference
        var collection = await GetCollectionAsync(bucket, scopeName, collectionName);


        await collection.InsertAsync(documentId, document);
    }

    [LibraryMethod]
    public async Task RemoveTestDocument(string scopeName, string collectionName, string documentId)
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;
        // get a user-defined collection reference
        var collection = await GetCollectionAsync(bucket, scopeName, collectionName);

        await collection.RemoveAsync(documentId);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task Get(string scopeName, string collectionName, string documentId)
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;

        // get a user-defined collection reference
        var collection = await GetCollectionAsync(bucket, scopeName, collectionName);

        // get a document
        using var getResult1 = await collection.GetAsync(documentId);
        using var getResult2 = await collection.GetAnyReplicaAsync(documentId);
        // for some reason, this fails if one of the previous 2 methods isn't called first.
        var result = await Task.WhenAll(collection.GetAllReplicasAsync(documentId));
        foreach (var r in result)
            r.Dispose();
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task GetAndLockAndUnlock(string scopeName, string collectionName, string documentId)
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;

        // get a user-defined collection reference
        var collection = await GetCollectionAsync(bucket, scopeName, collectionName);

        // get a document and lock it
        using var result = await collection.GetAndLockAsync(documentId, TimeSpan.FromSeconds(10));

        // unlock the document
        await collection.UnlockAsync(documentId, result.Cas);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task Exists(string scopeName, string collectionName, string documentId)
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;

        // get a user-defined collection reference
        var collection = await GetCollectionAsync(bucket, scopeName, collectionName);
        // check if a document exists
        await collection.ExistsAsync(documentId);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task InsertUpsertReplaceAndRemove(string scopeName, string collectionName, string documentId, string base64EncodedSerializedInsertDocument, string base64EncodedSerializedUpsertDocument, string base64EncodedSerializedReplaceDocument)
    {
        var serializedInsertDocument = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64EncodedSerializedInsertDocument));
        var serializedUpsertDocument = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64EncodedSerializedUpsertDocument));
        var serializedReplaceDocument = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64EncodedSerializedReplaceDocument));

        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;

        // get a user-defined collection reference
        var collection = await GetCollectionAsync(bucket, scopeName, collectionName);

        // insert a document
        await collection.InsertAsync(documentId, JsonConvert.DeserializeObject(serializedInsertDocument));

        // upsert a document
        await collection.UpsertAsync(documentId, JsonConvert.DeserializeObject(serializedUpsertDocument));

        // replace a document
        await collection.ReplaceAsync(documentId, JsonConvert.DeserializeObject(serializedReplaceDocument));

        // delete the document
        await collection.RemoveAsync(documentId);
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
    public async Task Lookup(string scopeName, string collectionName, string documentId)
    {
        var initResponse = await InitializeAsync();
        await using var cluster = initResponse.Cluster;
        await using var bucket = initResponse.Bucket;

        // get a user-defined collection reference
        var collection = await GetCollectionAsync(bucket, scopeName, collectionName);

        // lookup a document
#if NET481_OR_GREATER || NET
        using var result1 = await collection.LookupInAsync(documentId, [LookupInSpec.Get("credit_cards")]);
        using var result2 = await collection.LookupInAnyReplicaAsync(documentId, [LookupInSpec.Get("credit_cards")]);
        var results = collection.LookupInAllReplicasAsync(documentId, [LookupInSpec.Get("credit_cards")]);
        await foreach (var result in results)
        {
            result.Dispose();
        }
#else
        await collection.LookupInAsync(documentId, [LookupInSpec.Get("credit_cards")]);
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
#endif
