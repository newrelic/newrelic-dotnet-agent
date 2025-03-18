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
                // might need to wait for the bucket to finish creating
                //System.Threading.Thread.Sleep(30000);
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

        //[MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        //public async Task<bool> DeleteStreamAsync(string name)
        //{
        //    var response = await _amazonKinesisClient.DeleteStreamAsync(new DeleteStreamRequest
        //    {
        //        StreamName = name,
        //        EnforceConsumerDeletion = true
        //    });

        //    return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        //}

        //[MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        //public async Task<bool> ListStreamsAsync()
        //{
        //    var response = await _amazonKinesisClient.ListStreamsAsync(new ListStreamsRequest());

        //    if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
        //    {
        //        response.StreamNames.ForEach(s => Console.WriteLine($"Found stream name: {s}"));
        //        return true;
        //    }
        //    else
        //    {
        //        return false;
        //    }
        //}

        //[MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        //public async Task<bool> RegisterStreamConsumerAsync(string streamName, string consumerName)
        //{
        //    var response = await _amazonKinesisClient.DescribeStreamAsync(new DescribeStreamRequest
        //    {
        //        StreamName = streamName
        //    });

        //    if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
        //    {
        //        var streamArn = response.StreamDescription.StreamARN;
        //        var registerConsumerResponse = await _amazonKinesisClient.RegisterStreamConsumerAsync(new RegisterStreamConsumerRequest
        //        {
        //            StreamARN = streamArn,
        //            ConsumerName = consumerName
        //        });

        //        if (registerConsumerResponse.HttpStatusCode == System.Net.HttpStatusCode.OK)
        //        {

        //            ConsumerStatus status = registerConsumerResponse.Consumer.ConsumerStatus;
        //            var consumerArn = registerConsumerResponse.Consumer.ConsumerARN;

        //            // Wait until the consumer is ACTIVE and then report success.
        //            Console.Write("Waiting for consumer to finish being created...");

        //            int sleepDuration = 2000;

        //            var startTime = DateTime.Now;
        //            do
        //            {
        //                await Task.Delay(sleepDuration);

        //                var describeStreamConsumerResponse = await _amazonKinesisClient.DescribeStreamConsumerAsync(new DescribeStreamConsumerRequest
        //                {
        //                    StreamARN = streamArn,
        //                    ConsumerName = consumerName

        //                });

        //                status = describeStreamConsumerResponse.ConsumerDescription.ConsumerStatus;

        //                Console.Write(".");
        //            }
        //            while (status != "ACTIVE" && DateTime.Now - startTime < TimeSpan.FromMinutes(2));

        //            return status == ConsumerStatus.ACTIVE;
        //        }
        //        return true;
        //    }
        //    else
        //    {
        //        return false;
        //    }
        //}

        //[MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        //public async Task<bool> DeregisterStreamConsumerAsync(string streamName, string consumerName)
        //{
        //    var streamArn = await GetStreamArn(streamName);
        //    if (streamArn != null)
        //    {
        //        var registerConsumerResponse = await _amazonKinesisClient.DeregisterStreamConsumerAsync(new DeregisterStreamConsumerRequest
        //        {
        //            StreamARN = streamArn,
        //            ConsumerName = consumerName
        //        });

        //        return registerConsumerResponse.HttpStatusCode == System.Net.HttpStatusCode.OK;
        //    }
        //    return false;
        //}

        //private async Task<string> GetStreamArn(string streamName)
        //{
        //    var response = await _amazonKinesisClient.DescribeStreamAsync(new DescribeStreamRequest
        //    {
        //        StreamName = streamName
        //    });

        //    if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
        //    {
        //        return response.StreamDescription.StreamARN;
        //    }
        //    return null;
        //}

        //    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        //public async Task<bool> ListStreamConsumersAsync(string streamName)
        //{
        //    var streamArn = await GetStreamArn(streamName);
        //    if (streamArn != null)
        //    {
        //        var response = await _amazonKinesisClient.ListStreamConsumersAsync(new ListStreamConsumersRequest
        //        {
        //            MaxResults = 10,
        //            StreamARN = streamArn
        //        });

        //        if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
        //        {
        //            response.Consumers.ForEach(c => Console.WriteLine($"Found consumer name: {c.ConsumerName}"));
        //            return true;
        //        }
        //        else
        //        {
        //            return false;
        //        }
        //    }
        //    return false;

        //}

        //[MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        //public async Task<bool> PutRecordAsync(string streamName, string data)
        //{
        //    var response = await _amazonKinesisClient.PutRecordAsync(new PutRecordRequest
        //    {
        //        StreamName = streamName,
        //        PartitionKey = "nrtest",
        //        Data = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(data))
        //    });

        //    return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        //}

        //[MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        //public async Task<bool> PutRecordsAsync(string streamName, string data)
        //{
        //    var response = await _amazonKinesisClient.PutRecordsAsync(new PutRecordsRequest
        //    {
        //        StreamName = streamName,
        //        Records = new System.Collections.Generic.List<PutRecordsRequestEntry>
        //        {
        //            new PutRecordsRequestEntry { PartitionKey = "nrtest", Data = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(data)) },
        //            new PutRecordsRequestEntry { PartitionKey = "nrtest", Data = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(data)) }
        //        }
        //    });

        //    return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        //}

        //[MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        //public async Task<bool> GetRecordsAsync(string streamName)
        //{
        //    //Step #1 - describe stream to find out the shards it contains
        //    DescribeStreamRequest describeRequest = new DescribeStreamRequest();
        //    describeRequest.StreamName = streamName;

        //    var describeResponse = await _amazonKinesisClient.DescribeStreamAsync(describeRequest);
        //    List<Shard> shards = describeResponse.StreamDescription.Shards;
        //    foreach (Shard s in shards)
        //    {
        //        Console.WriteLine("shard: " + s.ShardId);
        //    }

        //    //grab the only shard ID in this stream
        //    string primaryShardId = shards[0].ShardId;

        //    //Step #2 - get iterator for this shard
        //    var shardIteratorRequest = new GetShardIteratorRequest
        //    {
        //        StreamName = streamName,
        //        ShardId = primaryShardId,
        //        ShardIteratorType = ShardIteratorType.TRIM_HORIZON
        //    };
        //    var response = await _amazonKinesisClient.GetShardIteratorAsync(shardIteratorRequest);
        //    var iterator = response.ShardIterator;

        //    var request = new GetRecordsRequest
        //    {
        //        ShardIterator = iterator,
        //        Limit = 10
        //    };

        //    var getResponse = await _amazonKinesisClient.GetRecordsAsync(request);
        //    if (getResponse.HttpStatusCode == System.Net.HttpStatusCode.OK)
        //    {
        //        string nextIterator = getResponse.NextShardIterator;
        //        //retrieve records
        //        List<Record> records = getResponse.Records;

        //        //print out each record's data value
        //        foreach (Record r in records)
        //        {
        //            //pull out (JSON) data in this record
        //            string s = Encoding.UTF8.GetString(r.Data.ToArray());
        //            Console.WriteLine("Record: " + s);
        //            Console.WriteLine("Partition Key: " + r.PartitionKey);
        //        }
        //    }

        //    return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        //}


        public void Dispose()
        {
            _amazonKinesisFirehoseClient?.Dispose();
            _amazonS3Client?.Dispose();
        }
    }
}
