// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Mock.Amazon.Lambda.SNSEvents
{
    public class SNSEvent
    {
        public List<SNSEventRecord> Records { get; set; }
    }

    public class SNSEventRecord
    {
        public string EventSubscriptionArn { get; set; }
        public SNS Sns { get; set; }
    }

    public class SNS
    {
        public string MessageId { get; set; }
        public string Timestamp { get; set; }
        public string TopicArn { get; set; }
        public string Type { get; set; }
        public Dictionary<string, object> MessageAttributes { get; set; }
    }
}

