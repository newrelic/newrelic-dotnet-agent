// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;

namespace NewRelic.Mock.Amazon.Lambda.DynamoDBEvents
{
    //  https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.DynamoDBEvents/DynamoDBEvent.cs
    public class DynamoDBEvent
    {
        public IList<DynamodbStreamRecord> Records { get; set; }

        public class DynamodbStreamRecord
        {
            public string EventSourceArn { get; set; }

            public string AwsRegion { get; set; }

            public StreamRecord Dynamodb { get; set; }

            public string EventID { get; set; }

            public string EventName { get; set; }

            public string EventSource { get; set; }

            public string EventVersion { get; set; }

            public Identity UserIdentity { get; set; }
        }

        public class StreamRecord
        {
            public DateTime ApproximateCreationDateTime { get; set; }

            public Dictionary<string, AttributeValue> Keys { get; set; }

            public Dictionary<string, AttributeValue> NewImage { get; set; }

            public Dictionary<string, AttributeValue> OldImage { get; set; }

            public string SequenceNumber { get; set; }

            public long SizeBytes { get; set; }

            public string StreamViewType { get; set; }
        }

        public class Identity
        {
            public string PrincipalId { get; set; }

            public string Type { get; set; }
        }

        public class AttributeValue
        {
            public MemoryStream B { get; set; }

            public bool? BOOL { get; set; }

            public List<MemoryStream> BS { get; set; }

            public List<AttributeValue> L { get; set; }

            public Dictionary<string, AttributeValue> M { get; set; }

            public string N { get; set; }

            public List<string> NS { get; set; }

            public bool? NULL { get; set; }

            public string S { get; set; }

            public List<string> SS { get; set; }
        }
    }
}
