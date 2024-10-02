// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.SQS.Model;

namespace AwsSdkTestApp.SQSBackgroundService
{
    public interface ISQSReceiverService
    {
        Task<IEnumerable<Message>> ReceiveAMessageAsync(string queueUrl);
    }
}
