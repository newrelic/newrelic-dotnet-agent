// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Mock.Amazon.Lambda.KinesisEvents
{
    /// <summary>
    /// https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.KinesisEvents/KinesisEvent.cs
    /// </summary>
    public class KinesisEvent
    {
        public List<KinesisEventRecord> Records { get; set; }

        public class KinesisEventRecord
        {
            public string EventSourceARN { get; set; }
            public string AwsRegion { get; set; }
        }
    }
}
