// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB
{
    [Library]
    public class CosmosDBExerciser
    {
        //This configuration is necessary to bypass the SSL connection error when testing with the CosmosDB server emulator without installing a cert.
        static readonly CosmosClientOptions _cosmosClientOptions = new CosmosClientOptions()
        {
            HttpClientFactory = () =>
            {
                HttpMessageHandler httpMessageHandler = new HttpClientHandler()
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                return new HttpClient(httpMessageHandler);
            },
            ConnectionMode = ConnectionMode.Gateway
        };

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task CreateReadAndDeleteDatabase(string databaseId)
        {
            var endpoint = CosmosDBConfiguration.CosmosDBServer;
                var authKey = CosmosDBConfiguration.AuthKey;

            using CosmosClient client = new CosmosClient(endpoint, authKey, _cosmosClientOptions);
            var databaseResponse = await client.CreateDatabaseIfNotExistsAsync(databaseId);

            var database = databaseResponse.Database;

            // Read the database from Azure Cosmos
            var readResponse = await database.ReadAsync();

            // Create a container/collection
            await readResponse.Database.CreateContainerAsync("testContainer", "/pk");

            // Read database using GetDatabaseQueryIterator()
            using (FeedIterator<DatabaseProperties> iterator = client.GetDatabaseQueryIterator<DatabaseProperties>())
            {
                while (iterator.HasMoreResults)
                {
                    foreach (DatabaseProperties db in await iterator.ReadNextAsync())
                    {
                        Console.WriteLine(db.Id);
                    }
                }
            }

            // Read database using GetDatabaseStreamQueryIterator
            using (FeedIterator iterator = client.GetDatabaseQueryStreamIterator())
            {
                while (iterator.HasMoreResults)
                {
                    using ResponseMessage response = await iterator.ReadNextAsync();
                    using (StreamReader sr = new StreamReader(response.Content))
                    {
                        sr.ReadToEnd();
                    }
                }
            }

            // Delete the database from Azure Cosmos.
            await database.DeleteAsync();
        }


        [LibraryMethod]
        public static void StartAgent()
        {
            NewRelic.Api.Agent.NewRelic.StartAgent();
            //Get everything started up and time for initial Sample().
            Thread.Sleep(TimeSpan.FromSeconds(10));
        }

        [LibraryMethod]
        public static void Wait()
        {
            Thread.Sleep(TimeSpan.FromSeconds(70));
        }
    }
}
