// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Scripts;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB
{
    [Library]
    public class CosmosDBExerciser
    {
        static long globalDocCounter = 0;

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
                            if (string.IsNullOrEmpty(db.Id))
                            {
                                throw new Exception("db.Id was null or empty, but it should have a non-empty value.");
                            }
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
                            var a = await sr.ReadToEndAsync();
                            if (string.IsNullOrEmpty(a))
                            {
                                throw new Exception("Stream iterator result was null or empty, but we expected a non-empty value.");
                            }
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

                await ReadManyItems(container);
            }
            finally
            {
                await database.DeleteAsync();
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task CreateAndQueryItems(string databaseId, string containerId)
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

                await QueryItems(container);
            }
            finally
            {
                await database.DeleteAsync();
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task CreateAndExecuteStoredProc(string databaseId, string containerId)
        {
            Console.WriteLine("Tests CosmosDB stored procedures");

            var endpoint = CosmosDBConfiguration.CosmosDBServer;
            var authKey = CosmosDBConfiguration.AuthKey;

            using var client = new CosmosClient(endpoint, authKey, _cosmosClientOptions);
            var databaseResponse = await client.CreateDatabaseIfNotExistsAsync(databaseId);
            var database = databaseResponse.Database;

            try
            {
                Container container = await database.CreateContainerIfNotExistsAsync(containerId, "/pk");

                var cosmosScripts = container.Scripts;
                var scriptId = "HelloWorldStoredProc";

                Console.WriteLine("Creates HelloWorldStoredProc.js Stored procedure");

                string storedProcJs;
#if NET6_0_OR_GREATER
                storedProcJs = await File.ReadAllTextAsync("NetStandardLibraries/CosmosDB/StoredProcedures/HelloWorldStoredProc.js");
#else
                storedProcJs = File.ReadAllText("NetStandardLibraries/CosmosDB/StoredProcedures/HelloWorldStoredProc.js");
#endif
                var sproc = await cosmosScripts.CreateStoredProcedureAsync(
                    new StoredProcedureProperties(
                        scriptId,
                        storedProcJs));

                Console.WriteLine("Executes HelloWorldStoredProc.js stored procedure");

                var response = await container.Scripts.ExecuteStoredProcedureAsync<string>(
                    scriptId, new PartitionKey(1), null);

                if (!string.Equals("Hello, World", response.Resource))
                {
                    throw new Exception("Failed to execute HelloWorldStoredProc.js stored procedure.");
                }

            }
            finally
            {
                await database.DeleteAsync();
            }

        }

        private static async Task QueryItems(Container container)
        {
            QueryDefinition query = new QueryDefinition("SELECT * FROM SalesOrders s WHERE s.AccountNumber = 'Account1' AND s.TotalDue > 0");

            // GetItemQueryIterator
            using var resultSet = container.GetItemQueryIterator<SalesOrder>(
                queryDefinition: query,
                requestOptions: new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey("Account1")
                });
            while (resultSet.HasMoreResults)
            {
                var response = await resultSet.ReadNextAsync();

                if (response.Count != 4)
                {
                    throw new Exception($"Expected a value of 4 but got {response.Count}");
                }

                Console.WriteLine($"\n Account Number: {response.First().AccountNumber}; total due: {response.First().TotalDue};");
            }

            // GetItemQueryStreamIterator
            using var queryStreamIterator = container.GetItemQueryStreamIterator(
                queryDefinition: query,
                continuationToken: null,
                requestOptions: new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey("Account1")
                });
            while (queryStreamIterator.HasMoreResults)
            {
                using (var response = await queryStreamIterator.ReadNextAsync())
                {
                    using (StreamReader sr = new StreamReader(response.Content))
                    using (JsonTextReader jtr = new JsonTextReader(sr))
                    {
                        JObject result = await JObject.LoadAsync(jtr);

                        if (result == null)
                        {
                            throw new Exception("Expected a non-null result.");
                        }

                        Console.WriteLine($"\n Query returned {result["Documents"].Count()} documents.");
                    }
                }
            }
        }

        /// <summary>
        /// This testing method asynchrously creates n numbers of items as quickly as possible in CosmosBD using 2 workers. 
        /// </summary>
        /// <param name="databaseId"></param>
        /// <param name="containerId"></param>
        /// <returns></returns>
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static async Task CreateItemsConcurrentlyAsync(string databaseId, string containerId, int itemsToCreate)
        {

            var endpoint = CosmosDBConfiguration.CosmosDBServer;
            var authKey = CosmosDBConfiguration.AuthKey;

            _cosmosClientOptions.AllowBulkExecution = true;

            using var client = new CosmosClient(endpoint, authKey, _cosmosClientOptions);
            var databaseResponse = await client.CreateDatabaseIfNotExistsAsync(databaseId);
            var database = databaseResponse.Database;

            try
            {
                Container container = await database.CreateContainerAsync(containerId, "/AccountNumber");

                var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(30 * 1000);
                CancellationToken cancellationToken = cancellationTokenSource.Token;

                var stopwatch = Stopwatch.StartNew();
                long startMilliseconds = stopwatch.ElapsedMilliseconds;

                try
                {
                    var documentsToImportInBatch = new ConcurrentQueue<KeyValuePair<PartitionKey, Stream>>();

                    for (int i = 0; i < itemsToCreate; i++)
                    {
                        var salesOrder = GetSalesOrderSample($"SalesOrder{i}");
                        documentsToImportInBatch.Enqueue(new KeyValuePair<PartitionKey, Stream>(new PartitionKey(salesOrder.AccountNumber), ToStream(salesOrder)));
                    }

                    var workerTasks = new List<Task>();
                    var numWorkers = 2;

                    for (int i = 0; i < numWorkers; i++)
                    {
                        workerTasks.Add(Task.Run(() =>
                        {
                            while (!cancellationToken.IsCancellationRequested && Interlocked.Read(ref globalDocCounter) < itemsToCreate)
                            {
                                if (documentsToImportInBatch.TryDequeue(out var item))
                                {
                                    _ = container.CreateItemStreamAsync(item.Value, item.Key, null, cancellationToken)
                                    .ContinueWith((Task<ResponseMessage> task) =>
                                    {
                                        if (task.IsCompleted)
                                        {
                                            Interlocked.Increment(ref globalDocCounter);
                                        }
                                        task.Dispose();
                                    });
                                }
                            }
                        }));
                    }

                    await Task.WhenAll(workerTasks);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
            finally
            {
                _cosmosClientOptions.AllowBulkExecution = false;
                await database.DeleteAsync();
                Interlocked.Exchange(ref globalDocCounter, 0);
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

                if (response.Count != 4)
                {
                    throw new Exception($"Expected a value of 4 but got {response.Count}");
                }

                Console.WriteLine($"\n Account Number: {response.First().AccountNumber}; total sales: {response.Count};");
            }
        }

        private static async Task ReadManyItems(Container container)
        {
            // Create item list with (id, pkvalue) tuples
            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>
            {
                ("SalesOrder1", new PartitionKey("Account1")),
                ("SalesOrder2", new PartitionKey("Account1")),
            };

            var feedResponse = await container.ReadManyItemsAsync<SalesOrder>(itemList);


            if (feedResponse.Count != 2)
            {
                throw new Exception($"Expected a value of 2 but got {feedResponse.Count}");
            }

            Console.WriteLine($"\n Account Number: {feedResponse.First().AccountNumber}; total sales: {feedResponse.Count};");

            // ReadManyStreamApi
            using var responseMessage = await container.ReadManyItemsStreamAsync(itemList);

            if (responseMessage.IsSuccessStatusCode)
            {
                dynamic streamResponse = FromStream<dynamic>(responseMessage.Content); 
                var salesOrders = streamResponse.Documents.ToObject<List<SalesOrder>>();
                if (salesOrders.Count != 2)
                {
                    throw new Exception($"Expected a value of 2 but got {salesOrders.Count}");
                }
            }
            else
            {
                throw new Exception($"ReadManyItemsStreamAsync() failed. Status code: {responseMessage.StatusCode} Message: {responseMessage.ErrorMessage}");
            }
        }

        private static async Task CreateItemsAsync(Container container)
        {
            Console.WriteLine("\n Creating items");

            var salesOrder = GetSalesOrderSample("SalesOrder1");
            var response1 = await container.CreateItemAsync(salesOrder, new PartitionKey(salesOrder.AccountNumber));

            if (response1.StatusCode != System.Net.HttpStatusCode.Created)
            {
                throw new Exception($"Expected a value of {System.Net.HttpStatusCode.Created} but got {response1.StatusCode}");
            }


            var salesOrder2 = GetSalesOrderSample("SalesOrder2");
            var response2 = await container.CreateItemStreamAsync(ToStream(salesOrder2), new PartitionKey(salesOrder2.AccountNumber));

            if (response2.IsSuccessStatusCode)
            {
                _ = FromStream<SalesOrder>(response2.Content);
            }
            else
            {
                throw new Exception($"Create item from stream failed. Status code: {response2.StatusCode} Message: {response2.ErrorMessage}");
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

            if (response.StatusCode != System.Net.HttpStatusCode.Created)
            {
                throw new Exception($"Expected a value of {System.Net.HttpStatusCode.Created} but got {response.StatusCode}");
            }

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
