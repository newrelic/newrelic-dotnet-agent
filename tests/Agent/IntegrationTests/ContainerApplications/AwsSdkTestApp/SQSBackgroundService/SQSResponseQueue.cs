// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Amazon.SQS.Model;

namespace AwsSdkTestApp.SQSBackgroundService
{
    public class SQSResponseQueue : ISQSResponseQueue
    {
        private readonly Channel<IEnumerable<Message>> _responseQueue;

        public SQSResponseQueue()
        {
            var options = new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            _responseQueue = Channel.CreateBounded<IEnumerable<Message>>(options);
        }

        public async Task QueueResponseAsync(IEnumerable<Message> messages)
        {
            await _responseQueue.Writer.WriteAsync(messages);
        }

        public async Task<IEnumerable<Message>> DequeueAsync(CancellationToken cancellationToken)
        {
            return await _responseQueue.Reader.ReadAsync(cancellationToken);
        }
    }
}
