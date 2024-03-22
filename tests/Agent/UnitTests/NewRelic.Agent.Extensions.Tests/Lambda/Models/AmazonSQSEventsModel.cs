// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Mock.Amazon.Lambda.SQSEvents
{
    public class SQSEvent
    {
        public List<SQSEventRecord> Records { get; set; }
    }

    public class SQSEventRecord
    {
        public string EventSourceArn { get; set; }
        public Dictionary<string, object> MessageAttributes { get; set; }
        public string Body { get; set; }
    }
}
