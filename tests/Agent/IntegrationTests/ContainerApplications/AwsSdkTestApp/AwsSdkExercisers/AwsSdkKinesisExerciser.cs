// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System;
using Amazon.Runtime;
using Amazon.Kinesis;
using Amazon.Kinesis.Model;

namespace AwsSdkTestApp.AwsSdkExercisers
{
    public class AwsSdkKinesisExerciser : IDisposable
    {
        private readonly AmazonKinesisClient _amazonKinesisClient;

        public AwsSdkKinesisExerciser()
        {
            _amazonKinesisClient = GetKinesisClient();
        }
        

        private AmazonKinesisClient GetKinesisClient()
        {
            // configure the client to use LocalStack
            // use plausible (but fake) access key and fake secret key so account id parsing can be tested
            var creds = new BasicAWSCredentials("FOOIHSHSDNNAEXAMPLE", "MOREGIBBERISH");
            var config = new AmazonKinesisConfig
            {
                ServiceURL = "http://localstack:4566",
                AuthenticationRegion = "us-west-2"
            };

            var client = new AmazonKinesisClient(creds, config);
            return client;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> CreateStreamAsync(string name)
        {
            var response = await _amazonKinesisClient.CreateStreamAsync(new CreateStreamRequest
            {
                StreamName = name,
                ShardCount = 1
            });

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                var request = new DescribeStreamRequest
                {
                    StreamName = name
                };

                StreamStatus status;

                // Wait until the stream is ACTIVE and then report success.
                Console.Write("Waiting for stream to finish being created...");

                int sleepDuration = 2000;

                var startTime = DateTime.Now;
                do
                {
                    await Task.Delay(sleepDuration);

                    var describeStreamResponse = await _amazonKinesisClient.DescribeStreamAsync(request);
                    status = describeStreamResponse.StreamDescription.StreamStatus;

                    Console.Write(".");
                }
                while (status != "ACTIVE" && DateTime.Now - startTime < TimeSpan.FromMinutes(2));

                return status == StreamStatus.ACTIVE;
            }
            else
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> ListStreamsAsync()
        {
            var response = await _amazonKinesisClient.ListStreamsAsync(new ListStreamsRequest());

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                response.StreamNames.ForEach(s => Console.WriteLine($"Found stream name: {s}"));
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Dispose()
        {
            _amazonKinesisClient?.Dispose();
        }
    }
}
