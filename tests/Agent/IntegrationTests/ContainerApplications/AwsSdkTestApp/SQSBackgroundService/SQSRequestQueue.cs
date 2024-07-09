// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AwsSdkTestApp.SQSBackgroundService
{
    public class SQSRequestQueue : ISQSRequestQueue
    {
        private readonly Channel<string> _requestQueue;

        public SQSRequestQueue()
        {
            var options = new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            _requestQueue = Channel.CreateBounded<string>(options);
        }

        public async Task QueueRequestAsync(string queueUrl)
        {
            await _requestQueue.Writer.WriteAsync(queueUrl);
        }

        public async Task<string> DequeueAsync(CancellationToken cancellationToken)
        {
            return await _requestQueue.Reader.ReadAsync(cancellationToken);
        }
    }
}
