// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Mock.Amazon.Lambda.SNSEvents
{
    // https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.SNSEvents/SNSEvent.cs
    public class SNSEvent
    {
        public List<SNSRecord> Records { get; set; }

        public class SNSRecord
        {
            public string EventSubscriptionArn { get; set; }
            public SNSMessage Sns { get; set; }
        }

        public class SNSMessage
        {
            public string MessageId { get; set; }
            public DateTime Timestamp { get; set; }
            public string TopicArn { get; set; }
            public string Type { get; set; }
            public Dictionary<string, MessageAttribute> MessageAttributes { get; set; }
        }
        public class MessageAttribute
        {
            public string Type { get; set; }
            public string Value { get; set; }
        }
    }
}

