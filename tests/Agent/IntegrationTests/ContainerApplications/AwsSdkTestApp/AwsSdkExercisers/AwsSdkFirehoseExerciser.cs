// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System;
using Amazon.Runtime;
using Amazon.KinesisFirehose;
using Amazon.KinesisFirehose.Model;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text;

namespace AwsSdkTestApp.AwsSdkExercisers
{
    public class AwsSdkFirehoseExerciser : IDisposable
    {
        private readonly AmazonKinesisFirehoseClient _amazonKinesisFirehoseClient;
        private readonly AmazonS3Client _amazonS3Client;

        public AwsSdkFirehoseExerciser()
        {
            _amazonKinesisFirehoseClient = GetKinesisFirehoseClient();
            _amazonS3Client = GetS3Client();
        }


        private AmazonKinesisFirehoseClient GetKinesisFirehoseClient()
        {
            // configure the client to use LocalStack
            // use plausible (but fake) access key and fake secret key so account id parsing can be tested
            var creds = new BasicAWSCredentials("FOOIHSHSDNNAEXAMPLE", "MOREGIBBERISH");
            var config = new AmazonKinesisFirehoseConfig
            {
                ServiceURL = "http://localstack:4566",
                AuthenticationRegion = "us-west-2"
            };

            var client = new AmazonKinesisFirehoseClient(creds, config);
            return client;
        }
        private AmazonS3Client GetS3Client()
        {
            // configure the client to use LocalStack
            // use plausible (but fake) access key and fake secret key so account id parsing can be tested
            var creds = new BasicAWSCredentials("FOOIHSHSDNNAEXAMPLE", "MOREGIBBERISH");
            var config = new AmazonS3Config
            {
                ServiceURL = "http://localstack:4566",
                AuthenticationRegion = "us-west-2",
                ForcePathStyle = true
            };

            var client = new AmazonS3Client(creds, config);
            return client;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> CreateDeliveryStreamAsync(string streamName, string bucketName)
        {
            // First we need to create an s3 bucket to configure as the delivery destination
            var createS3BucketResponse = await _amazonS3Client.PutBucketAsync(new PutBucketRequest
            {
                BucketName = bucketName,
                BucketRegion = "us-west-2"
            });

            if (createS3BucketResponse.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                var s3DestinationConfiguration = new ExtendedS3DestinationConfiguration
                {
                    BucketARN = "arn:aws:s3:::" + bucketName,
                    RoleARN = "arn:aws:iam::000000000000:role/Firehose-Reader-Role" // per Localstack docs: https://docs.localstack.cloud/user-guide/aws/firehose/
                };
                var response = await _amazonKinesisFirehoseClient.CreateDeliveryStreamAsync(new CreateDeliveryStreamRequest
                {
                    DeliveryStreamName = streamName,
                    ExtendedS3DestinationConfiguration = s3DestinationConfiguration
                });

                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    var request = new DescribeDeliveryStreamRequest
                    {
                        DeliveryStreamName = streamName
                    };

                    DeliveryStreamStatus status;

                    // Wait until the stream is ACTIVE and then report success.
                    Console.Write("Waiting for stream to finish being created...");

                    int sleepDuration = 2000;

                    var startTime = DateTime.Now;
                    do
                    {
                        await Task.Delay(sleepDuration);

                        var describeStreamResponse = await _amazonKinesisFirehoseClient.DescribeDeliveryStreamAsync(request);
                        status = describeStreamResponse.DeliveryStreamDescription.DeliveryStreamStatus;

                        Console.Write(".");
                    }
                    while (status != "ACTIVE" && DateTime.Now - startTime < TimeSpan.FromMinutes(2));

                    return status == DeliveryStreamStatus.ACTIVE;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> DeleteDeliveryStreamAsync(string name)
        {
            var response = await _amazonKinesisFirehoseClient.DeleteDeliveryStreamAsync(new DeleteDeliveryStreamRequest
            {
                DeliveryStreamName = name,
                AllowForceDelete = true
            });

            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> ListDeliveryStreamsAsync()
        {
            var response = await _amazonKinesisFirehoseClient.ListDeliveryStreamsAsync(new ListDeliveryStreamsRequest());

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                response.DeliveryStreamNames.ForEach(s => Console.WriteLine($"Found stream name: {s}"));
                return true;
            }
            else
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> PutRecordAsync(string streamName, string data)
        {
            var response = await _amazonKinesisFirehoseClient.PutRecordAsync(new PutRecordRequest
            {
                DeliveryStreamName = streamName,
                Record = new Record
                {
                    Data = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(data))
                }
            });

            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> PutRecordBatchAsync(string streamName, string data)
        {
            var response = await _amazonKinesisFirehoseClient.PutRecordBatchAsync(new PutRecordBatchRequest
            {
                DeliveryStreamName = streamName,
                Records = new System.Collections.Generic.List<Record>
                {
                    new Record { Data = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(data)) },
                    new Record { Data = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(data)) }
                }
            });

            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }

        public void Dispose()
        {
            _amazonKinesisFirehoseClient?.Dispose();
            _amazonS3Client?.Dispose();
        }
    }
}
