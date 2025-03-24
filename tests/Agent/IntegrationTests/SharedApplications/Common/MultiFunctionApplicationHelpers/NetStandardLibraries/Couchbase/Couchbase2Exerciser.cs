// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET462
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Couchbase;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Search;
using Couchbase.Search.Queries.Simple;
using NewRelic.Agent.IntegrationTests.Shared.Couchbase;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using Newtonsoft.Json;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Couchbase;

/// <summary>
/// Executes a subset (mostly Async) of the Couchbase 2.0 API
/// </summary>
[Library]
class Couchbase2Exerciser
{
    private async Task<(Cluster Cluster, IBucket Bucket)> InitializeAsync()
    {

        var config = new ClientConfiguration();
        config.Servers = new List<Uri>()
        {
            new Uri(CouchbaseTestObject.CouchbaseServerUrl)
        };
        config.UseSsl = false;
        var authenticator = new PasswordAuthenticator(CouchbaseTestObject.Username, CouchbaseTestObject.Password);

        ClusterHelper.Initialize(config, authenticator);
        var cluster = ClusterHelper.Get();

        var bucket = await cluster.OpenBucketAsync(CouchbaseTestObject.CouchbaseTestBucket);

        return (cluster, bucket);
    }

    [LibraryMethod]
    public async Task InsertTestDocument(string documentId, string base64EncodedSerializedDocument)
    {
        var serializedDocument = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64EncodedSerializedDocument));
        var document = Newtonsoft.Json.JsonConvert.DeserializeObject(serializedDocument);

        var initResponse = await InitializeAsync();
        using var cluster = initResponse.Cluster;
        using var bucket = initResponse.Bucket;

        await bucket.InsertAsync(documentId, document);
    }

    [LibraryMethod]
    public async Task RemoveTestDocument(string documentId)
    {
        var initResponse = await InitializeAsync();
        using var cluster = initResponse.Cluster;
        using var bucket = initResponse.Bucket;

        await bucket.RemoveAsync(documentId);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task Get(string documentId)
    {
        var initResponse = await InitializeAsync();
        using var cluster = initResponse.Cluster;
        using var bucket = initResponse.Bucket;

        // get a document
        var getResult1 = await bucket.GetAsync<dynamic>(documentId);
        var getResult2 = await bucket.GetDocumentAsync<dynamic>(documentId);

#pragma warning disable VSTHRD103
        var getResult3 = bucket.Get<dynamic>(documentId);
#pragma warning restore VSTHRD103
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task GetMultiple(string documentIds)
    {
        var initResponse = await InitializeAsync();
        using var cluster = initResponse.Cluster;
        using var bucket = initResponse.Bucket;
        var ids = documentIds.Split(',');

#pragma warning disable CS0618 // Type or member is obsolete
        var getResults1 = bucket.Get<dynamic>(ids);
#pragma warning restore CS0618 // Type or member is obsolete
        var getResults2 = await bucket.GetDocumentsAsync<dynamic>(ids);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task GetAndLockAndUnlock(string documentId)
    {
        var initResponse = await InitializeAsync();
        using var cluster = initResponse.Cluster;
        using var bucket = initResponse.Bucket;

        // get a document and lock it
        var result = await bucket.GetAndLockAsync<dynamic>(documentId, TimeSpan.FromSeconds(10));

        // unlock the document
        await bucket.UnlockAsync(documentId, result.Cas);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task Exists(string documentId)
    {
        var initResponse = await InitializeAsync();
        using var cluster = initResponse.Cluster;
        using var bucket = initResponse.Bucket;

        // check if a document exists
        await bucket.ExistsAsync(documentId);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task InsertUpsertReplaceAndRemove(string documentId, string base64EncodedSerializedInsertDocument, string base64EncodedSerializedUpsertDocument, string base64EncodedSerializedReplaceDocument)
    {
        var serializedInsertDocument = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64EncodedSerializedInsertDocument));
        var serializedUpsertDocument = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64EncodedSerializedUpsertDocument));
        var serializedReplaceDocument = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64EncodedSerializedReplaceDocument));

        var initResponse = await InitializeAsync();
        using var cluster = initResponse.Cluster;
        using var bucket = initResponse.Bucket;

        // insert a document
        await bucket.InsertAsync(documentId, JsonConvert.DeserializeObject(serializedInsertDocument));

        // upsert a document
        await bucket.UpsertAsync(documentId, JsonConvert.DeserializeObject(serializedUpsertDocument));

        // replace a document
        await bucket.ReplaceAsync(documentId, JsonConvert.DeserializeObject(serializedReplaceDocument));

        // delete the document
        await bucket.RemoveAsync(documentId);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task Touch()
    {
        var initResponse = await InitializeAsync();
        using var cluster = initResponse.Cluster;
        using var bucket = initResponse.Bucket;

        // insert a new document so we can touch it and let it expire
        var key = Guid.NewGuid().ToString();
        await bucket.InsertAsync(key, new { Name = "Ted", Age = 32 });

        await bucket.TouchAsync(key, TimeSpan.FromSeconds(5));
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task BucketQuery()
    {
        var initResponse = await InitializeAsync();
        using var cluster = initResponse.Cluster;
        using var bucket = initResponse.Bucket;

#pragma warning disable VSTHRD103
        var clusterQueryResult1 = bucket.Query<dynamic>("SELECT t.* FROM `travel-sample` t LIMIT 10");
#pragma warning restore VSTHRD103
        var clusterQueryResult2 = await bucket.QueryAsync<dynamic>("SELECT t.* FROM `travel-sample` t LIMIT 10");
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task BucketSearch()
    {
        var initResponse = await InitializeAsync();
        using var cluster = initResponse.Cluster;
        using var bucket = initResponse.Bucket;
        var result = await bucket.QueryAsync(new SearchQuery
        {
            Index = "hotels",
            Query = new MatchQuery("Apex"),
            SearchParams = new SearchParams().Limit(10).Timeout(TimeSpan.FromMilliseconds(10000))
        });
    }
}
#endif
