// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.DynamoDBv2.Model;
using Amazon.DynamoDBv2;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using Amazon.Runtime;
using System.Threading;

namespace AwsSdkTestApp.AwsSdkExercisers
{
    public class AwsSdkDynamoDBExerciser : IDisposable
    {
        private readonly AmazonDynamoDBClient _amazonDynamoDBClient;
        //private string _dynamoDbURL = null;

        public AwsSdkDynamoDBExerciser()
        {
            _amazonDynamoDBClient = GetDynamoDBClient();
        }
        

        private AmazonDynamoDBClient GetDynamoDBClient()
        {

            AmazonDynamoDBConfig clientConfig = new AmazonDynamoDBConfig();
            // Set the endpoint URL
            clientConfig.ServiceURL = "http://dynamodb:8000";
            clientConfig.AuthenticationRegion = "us-west-2";
            var creds = new BasicAWSCredentials("xxx", "xxx");
            AmazonDynamoDBClient client = new AmazonDynamoDBClient(creds, clientConfig);

            return client;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> CreateTableAsync(string name)
        {
            Console.WriteLine("Got here 1");
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
            Console.WriteLine("Got here 2");
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

            do
            {
                Thread.Sleep(sleepDuration);

                var describeTableResponse = await _amazonDynamoDBClient.DescribeTableAsync(request);
                status = describeTableResponse.Table.TableStatus;

                Console.Write(".");
            }
            while (status != "ACTIVE");

            return status == TableStatus.ACTIVE;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> PutItemAsync(string tableName, string title, int year)
        {
            var newMovie = new Movie(title, year);
            var item = new Dictionary<string, AttributeValue>
            {
                ["title"] = new AttributeValue { S = newMovie.Title },
                ["year"] = new AttributeValue { N = newMovie.Year.ToString() },
            };

            var request = new PutItemRequest
            {
                TableName = tableName,
                Item = item,
            };

            var response = await _amazonDynamoDBClient.PutItemAsync(request);
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }

        public void Dispose()
        {
            _amazonDynamoDBClient?.Dispose();
        }
    }

    public class Movie
    {
        public Movie(string title, int year)
        {
            Title = title;
            Year = year;
        }

        public string Title { get; set; }
        public int Year { get; set; }
    }

}
