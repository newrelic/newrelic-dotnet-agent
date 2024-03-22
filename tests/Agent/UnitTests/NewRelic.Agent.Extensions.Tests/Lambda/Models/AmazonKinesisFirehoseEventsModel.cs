// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Mock.Amazon.Lambda.KinesisFirehoseEvents
{
    public class KinesisFirehoseEvent
    {
        public string DeliveryStreamArn { get; set; }
        public string Region { get; set; }
        public List<KinesisFirehoseEventRecord> Records { get; set; }
    }

    public class KinesisFirehoseEventRecord
    {
        public string Data { get; set; }
    }
}
