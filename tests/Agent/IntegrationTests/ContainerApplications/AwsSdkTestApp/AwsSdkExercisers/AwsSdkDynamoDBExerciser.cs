// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.DynamoDBv2.Model;
using Amazon.DynamoDBv2;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Amazon.Runtime;
using Amazon;

namespace AwsSdkTestApp.AwsSdkExercisers
{
    public class AwsSdkDynamoDBExerciser : IDisposable
    {
        private readonly AmazonDynamoDBClient _amazonDynamoDBClient;

        public AwsSdkDynamoDBExerciser()
        {
            _amazonDynamoDBClient = GetDynamoDBClient();
        }
        

        private AmazonDynamoDBClient GetDynamoDBClient()
        {

            AmazonDynamoDBConfig clientConfig = new AmazonDynamoDBConfig
            {
                // Set the endpoint URL
                ServiceURL = "http://dynamodb:8000", // port must match what is set in docker compose
                AuthenticationRegion = "us-west-2",
                RegionEndpoint = RegionEndpoint.USWest2
            };

            // use plausible (but fake) access key and fake secret key so account id parsing can be tested
            AmazonDynamoDBClient client = new AmazonDynamoDBClient("FOOIHSFODNNAEXAMPLE", "MOREGIBBERISH", clientConfig);

            return client;
        }

        #region Table Operations
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> CreateTableAsync(string name)
        {
            var response = await _amazonDynamoDBClient.CreateTableAsync(new CreateTableRequest
            {
                TableName = name,
                AttributeDefinitions = new List<AttributeDefinition>()
                {
                    new AttributeDefinition
                    {
                        AttributeName = "title",
                        AttributeType = ScalarAttributeType.S,
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "year",
                        AttributeType = ScalarAttributeType.N,
                    },
                },
                KeySchema = new List<KeySchemaElement>()
                {
                    new KeySchemaElement
                    {
                        AttributeName = "year",
                        KeyType = KeyType.HASH,
                    },
                    new KeySchemaElement
                    {
                        AttributeName = "title",
                        KeyType = KeyType.RANGE,
                    },
                },
                ProvisionedThroughput = new ProvisionedThroughput
                {
                    ReadCapacityUnits = 5,
                    WriteCapacityUnits = 5,
                },
            });
            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
            {
                Console.WriteLine($"Got bad http status code: {response.HttpStatusCode}");
            }

            // Wait until the table is ACTIVE and then report success.
            Console.Write("Waiting for table to become active...");

            var request = new DescribeTableRequest
            {
                TableName = response.TableDescription.TableName,
            };

            TableStatus status;

            int sleepDuration = 2000;

            var startTime = DateTime.Now;
            do
            {
                await Task.Delay(sleepDuration);

                var describeTableResponse = await _amazonDynamoDBClient.DescribeTableAsync(request);
                status = describeTableResponse.Table.TableStatus;

                Console.Write(".");
            }
            while (status != "ACTIVE" && DateTime.Now - startTime < TimeSpan.FromMinutes(2));

            return status == TableStatus.ACTIVE;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> DeleteTableAsync(string tableName)
        {
            var request = new DeleteTableRequest
            {
                TableName = tableName
            };
            var response = await _amazonDynamoDBClient.DeleteTableAsync(request);

            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }
        #endregion

        #region CRUD operations
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> PutItemAsync(string tableName, string title, string year)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                ["title"] = new AttributeValue { S = title },
                ["year"] = new AttributeValue { N = year },
            };

            var request = new PutItemRequest
            {
                TableName = tableName,
                Item = item,
            };

            var response = await _amazonDynamoDBClient.PutItemAsync(request);
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> GetItemAsync(string tableName, string title, string year)
        {
            var request = new GetItemRequest
            {
                TableName = tableName,
                Key = new Dictionary<string, AttributeValue>()
                { { "title", new AttributeValue { S = title } },
                  { "year",  new AttributeValue { N = year } }
                }
            };
            var response = await _amazonDynamoDBClient.GetItemAsync(request);

            // Check the response.
            var result = response.Item;

            Console.WriteLine($"GetItemAsync: response.Item['year'] == {result["year"]}");
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> UpdateItemAsync(string tableName, string title, string year)
        {
            var request = new UpdateItemRequest
            {
                TableName = tableName,
                Key = new Dictionary<string, AttributeValue>()
                { { "title", new AttributeValue { S = title } },
                  { "year",  new AttributeValue { N = year } }
                },
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    {"#NA", "Rating" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                {
                    { ":new", new AttributeValue { N = "5" } }
                },
                UpdateExpression = "SET #NA = :new"
            };
            var response = await _amazonDynamoDBClient.UpdateItemAsync(request);

            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> DeleteItemAsync(string tableName, string title, string year)
        {
            var request = new DeleteItemRequest
            {
                TableName = tableName,
                Key = new Dictionary<string, AttributeValue>()
                { { "title", new AttributeValue { S = title } },
                  { "year",  new AttributeValue { N = year } }
                },
            };
            var response = await _amazonDynamoDBClient.DeleteItemAsync(request);

            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }
        #endregion

        #region Query Operations
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> QueryAsync(string tableName, string title, string year)
        {
            var request = new QueryRequest
            {
                TableName = tableName,
                KeyConditionExpression = "#title = :title and #year = :year",
                ExpressionAttributeNames = new Dictionary<string, string>()
                {
                    {"#title", "title" },
                    {"#year", "year" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                {
                    {":title", new AttributeValue { S = title } },
                    {":year" , new AttributeValue { N = year } }
                }
            };
            var response = await _amazonDynamoDBClient.QueryAsync(request);

            Console.WriteLine($"QueryAsync: number of item returned = {response.Items.Count}");
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> ScanAsync(string tableName)
        {
            var request = new ScanRequest
            {
                TableName = tableName,
                Limit = 10

            };
            var response = await _amazonDynamoDBClient.ScanAsync(request);

            Console.WriteLine($"ScanAsync: number of item returned = {response.Items.Count}");
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }
        #endregion

        public void Dispose()
        {
            _amazonDynamoDBClient?.Dispose();
        }
    }
}
