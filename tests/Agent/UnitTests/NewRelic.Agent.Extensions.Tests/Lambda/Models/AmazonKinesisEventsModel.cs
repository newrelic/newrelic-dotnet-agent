// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Mock.Amazon.Lambda.KinesisEvents
{
    public class KinesisEvent
    {
        public List<KinesisEventRecord> Records { get; set; }
    }

    public class KinesisEventRecord
    {
        public string EventSourceArn { get; set; }
        public string AwsRegion { get; set; }
    }
}
