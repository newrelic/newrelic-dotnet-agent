// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading;
using System.Threading.Tasks;

namespace AwsSdkTestApp.SQSBackgroundService
{
    public interface ISQSRequestQueue
    {
        Task QueueRequestAsync(string queueUrl);

        Task<string> DequeueAsync(CancellationToken cancellationToken);
    }
}
