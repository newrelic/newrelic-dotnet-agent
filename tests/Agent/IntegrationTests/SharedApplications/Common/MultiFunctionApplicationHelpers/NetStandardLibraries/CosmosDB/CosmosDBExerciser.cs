// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using Newtonsoft.Json;
using Xunit;

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

        private static readonly JsonSerializer _serializer = new JsonSerializer();


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

            try
            {

                // Read the database from Azure Cosmos
                var readResponse = await database.ReadAsync();

                // Read database using GetDatabaseQueryIterator()
                using (FeedIterator<DatabaseProperties> iterator = client.GetDatabaseQueryIterator<DatabaseProperties>())
                {
                    while (iterator.HasMoreResults)
                    {
                        foreach (DatabaseProperties db in await iterator.ReadNextAsync())
                        {
                            Assert.False(string.IsNullOrEmpty(db.Id));
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
                            var a = sr.ReadToEnd();
                            Assert.False(string.IsNullOrEmpty(a));
                        }
                    }
                }
            }
            finally
            {
                // Delete the database from Azure Cosmos.
                await database.DeleteAsync();
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task CreateReadAndDeleteContainers(string databaseId, string containerId)
        {
            var endpoint = CosmosDBConfiguration.CosmosDBServer;
            var authKey = CosmosDBConfiguration.AuthKey;

            using var client = new CosmosClient(endpoint, authKey, _cosmosClientOptions);
            var databaseResponse = await client.CreateDatabaseIfNotExistsAsync(databaseId);
            var database = databaseResponse.Database;

            try
            {
                await database.CreateContainerAsync(containerId, "/pk");

                string queryText = "SELECT * FROM c";
                using FeedIterator<ContainerProperties> feedIterator = database.GetContainerQueryIterator<ContainerProperties>(queryText);
                while (feedIterator.HasMoreResults)
                {
                    var r = await feedIterator.ReadNextAsync();

                    foreach (var c in r)
                    {
                        var container = database.GetContainer(c.Id);
                        await container.ReadContainerAsync();
                        await container.DeleteContainerAsync();
                    }
                }
            }
            finally
            {
                await database.DeleteAsync();
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task CreateAndReadItems(string databaseId, string containerId)
        {
            var endpoint = CosmosDBConfiguration.CosmosDBServer;
            var authKey = CosmosDBConfiguration.AuthKey;

            using var client = new CosmosClient(endpoint, authKey, _cosmosClientOptions);
            var databaseResponse = await client.CreateDatabaseIfNotExistsAsync(databaseId);
            var database = databaseResponse.Database;

            try
            {
                Container container = await database.CreateContainerIfNotExistsAsync(containerId, "/AccountNumber");

                await CreateItemsAsync(container);

                await UpsertItemsAsync(container);

                await ReadAllItems(container);
            }
            finally
            {
                await database.DeleteAsync();
            }
        }

        private static async Task ReadAllItems(Container container)
        {
            using var resultSet = container.GetItemQueryIterator<SalesOrder>(
                queryDefinition: null,
                requestOptions: new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey("Account1")
                });
            while (resultSet.HasMoreResults)
            {
                var response = await resultSet.ReadNextAsync();
                Assert.True(response.Count == 4);
                Console.WriteLine($"\n Account Number: {response.First().AccountNumber}; total sales: {response.Count};");
            }
        }

        private static async Task CreateItemsAsync(Container container)
        {
            Console.WriteLine("\n Creating items");

            var salesOrder = GetSalesOrderSample("SalesOrder1");
            var response1 = await container.CreateItemAsync(salesOrder, new PartitionKey(salesOrder.AccountNumber));
            Assert.True(response1.StatusCode == System.Net.HttpStatusCode.Created);


            var salesOrder2 = GetSalesOrderSample("SalesOrder2");
            var response2 = await container.CreateItemStreamAsync(ToStream(salesOrder2), new PartitionKey(salesOrder2.AccountNumber));

            if (response2.IsSuccessStatusCode)
            {
                _ = FromStream<SalesOrder>(response2.Content);
            }
        }

        private static async Task UpsertItemsAsync(Container container)
        {
            Console.WriteLine("\n Upserting items");

            var upsertOrder = GetSalesOrderSample("SalesOrder3");

            //creates the initial SalesOrder document. 
            //notice the response.StatusCode returned indicates a Create operation was performed
            var response = await container.UpsertItemAsync(
                partitionKey: new PartitionKey(upsertOrder.AccountNumber),
                item: upsertOrder);

            Assert.True(response.StatusCode == System.Net.HttpStatusCode.Created);

            // For better performance upsert a SalesOrder object from a stream. 
            SalesOrder salesOrderV4 = GetSalesOrderSample("SalesOrder4");
            using (var stream = ToStream(salesOrderV4))
            {
                using (ResponseMessage responseMessage = await container.UpsertItemStreamAsync(
                    partitionKey: new PartitionKey(salesOrderV4.AccountNumber),
                    streamPayload: stream))
                {
                    // Item stream operations do not throw exceptions for better performance
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        _ = FromStream<SalesOrder>(responseMessage.Content);
                    }
                    else
                    {
                        Console.WriteLine($"Upsert item from stream failed. Status code: {responseMessage.StatusCode} Message: {responseMessage.ErrorMessage}");
                    }
                }
            }
        }

        private static Stream ToStream<T>(T input)
        {
            var streamPayload = new MemoryStream();
            using (var streamWriter = new StreamWriter(streamPayload, encoding: Encoding.Default, bufferSize: 1024, leaveOpen: true))
            {
                using var writer = new JsonTextWriter(streamWriter);
                writer.Formatting = Formatting.None;
                _serializer.Serialize(writer, input);
                writer.Flush();
                streamWriter.Flush();
            }

            streamPayload.Position = 0;
            return streamPayload;
        }

        private static T FromStream<T>(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }

            using (StreamReader sr = new StreamReader(stream))
            using (JsonTextReader jsonTextReader = new JsonTextReader(sr))
            {
                return _serializer.Deserialize<T>(jsonTextReader);
            }
        }



        private static SalesOrder GetSalesOrderSample(string itemId)
        {
            SalesOrder salesOrder = new SalesOrder
            {
                Id = itemId,
                AccountNumber = "Account1",
                PurchaseOrderNumber = "PO18009186470",
                OrderDate = new DateTime(2005, 7, 1),
                SubTotal = 419.4589m,
                TaxAmount = 12.5838m,
                Freight = 472.3108m,
                TotalDue = 985.018m,
                Items = new SalesOrderDetail[]
                {
                    new SalesOrderDetail
                    {
                        OrderQty = 1,
                        ProductId = 760,
                        UnitPrice = 419.4589m,
                        LineTotal = 419.4589m
                    }
                },
            };

            // Set the "ttl" property to auto-expire sales orders in 30 days 
            salesOrder.TimeToLive = 60 * 60 * 24 * 30;

            return salesOrder;
        }

        [LibraryMethod]
        public static void StartAgent()
        {
            NewRelic.Api.Agent.NewRelic.StartAgent();
            //Get everything started up and time for initial Sample().
            Thread.Sleep(TimeSpan.FromSeconds(10));
        }
    }
}
