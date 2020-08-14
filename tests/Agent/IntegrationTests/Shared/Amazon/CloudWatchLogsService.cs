// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using Amazon.CloudWatchLogs;
using Amazon.Runtime.CredentialManagement;
using Amazon.Runtime;
using Amazon.CloudWatchLogs.Model;
using Amazon;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using System.Threading;

namespace NewRelic.Agent.IntegrationTests.Shared.Amazon
{
    public class CloudWatchLogsService
    {
        private const string NRLogIdentifier = "NR_LAMBDA_MONITORING";
        private const string ProfileName = "awsunittest";
        private readonly RegionEndpoint Region = RegionEndpoint.USWest2;
        private readonly CredentialProfileStoreChain _credentialProfileStoreChain = new CredentialProfileStoreChain();
        private AWSCredentials _awsCredentials;

        private AWSCredentials AwsCredentials
        {
            get
            {
                if (_awsCredentials == null)
                {
                    _credentialProfileStoreChain.TryGetAWSCredentials(ProfileName, out _awsCredentials);
                }

                return _awsCredentials;
            }
        }

        public async Task<List<string>> GetCloudWatchEventMessagesForLogGroup(string logGroupName, DateTime startTime)
        {
            var result = new List<string>();
            var stopWatch = new Stopwatch();

            try
            {
                var client = new AmazonCloudWatchLogsClient(AwsCredentials, Region);

                var logStreamRequest = new DescribeLogStreamsRequest()
                {
                    LogGroupName = logGroupName,
                    OrderBy = OrderBy.LastEventTime
                };

                stopWatch.Start();

                while (stopWatch.ElapsedMilliseconds < TimeSpan.FromMinutes(1).TotalMilliseconds)
                {
                    var logStreamsResponse = await client.DescribeLogStreamsAsync(logStreamRequest);  // rate limit is 5 per second
                    foreach (var stream in logStreamsResponse.LogStreams)
                    {
                        var logEventsRequest = new GetLogEventsRequest()
                        {
                            LogGroupName = logGroupName,
                            LogStreamName = stream.LogStreamName,
                            StartTime = startTime,
                        };

                        var logEventsResponse = await client.GetLogEventsAsync(logEventsRequest); // rate limit is 10 per second
                        result.AddRange(logEventsResponse.Events.Select(e => e.Message).ToList());
                        Thread.Sleep(150);
                    }

                    if (result.Count > 0)
                    {
                        var nrLog = result.Where(l => l.Contains(NRLogIdentifier));
                        if (nrLog?.Count() > 0)
                        {
                            break;
                        }
                    }

                    Thread.Sleep(250);
                }
            }
            catch (Exception e)
            {
                throw e;
            }

            stopWatch.Stop();
            return result;
        }

        public async Task DeleteCloudWatchLogStreamsForLogStreams(string logGroupName)
        {
            try
            {
                var client = new AmazonCloudWatchLogsClient(AwsCredentials, Region);
                var logStreamRequest = new DescribeLogStreamsRequest()
                {
                    LogGroupName = logGroupName,
                    OrderBy = OrderBy.LastEventTime
                };

                var logStreamsResponse = await client.DescribeLogStreamsAsync(logStreamRequest);  // rate limit is 5 per second
                foreach (var stream in logStreamsResponse.LogStreams)
                {
                    var request = new DeleteLogStreamRequest(logGroupName, stream.LogStreamName);
                    var deleteLogStringresponse = await client.DeleteLogStreamAsync(request);
                    Thread.Sleep(150);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
