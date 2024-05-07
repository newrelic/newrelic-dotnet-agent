// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Mock.Amazon.Lambda.SNSEvents;

namespace NewRelic.Mock.Amazon.Lambda.SQSEvents
{
    //  https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.SQSEvents/SQSEvent.cs
    public class SQSEvent
    {
        public List<SQSMessage> Records { get; set; }

        public class SQSMessage
        {
            public string EventSourceArn { get; set; }
            public Dictionary<string, MessageAttribute> MessageAttributes { get; set; }
            public string Body { get; set; }
            public string MessageId { get; set; }
        }

        public class MessageAttribute
        {
            public string StringValue { get; set; }
        }
    }
}
