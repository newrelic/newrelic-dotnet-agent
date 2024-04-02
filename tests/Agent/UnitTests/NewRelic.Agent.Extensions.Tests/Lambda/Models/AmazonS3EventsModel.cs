// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Mock.Amazon.Lambda.S3Events
{
    //https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.S3Events/S3Event.cs
    public class S3Event
    {
        public List<S3EventNotificationRecord> Records { get; set; }

        public class S3EventNotificationRecord
        {
            public string AwsRegion { get; set; }
            public DateTime EventTime { get; set; }
            public string EventName { get; set; }
            public S3Entity S3 { get; set; }
            public ResponseElementsEntity ResponseElements { get; set; }
        }

        public class S3Entity
        {
            public S3BucketEntity Bucket { get; set; }
            public S3ObjectEntity Object { get; set; }
        }

        public class S3BucketEntity
        {
            public string Name { get; set; }
            public string Arn { get; set; }
        }

        public class S3ObjectEntity
        {
            public string Key { get; set; }
            public long Size { get; set; }
            public string Sequencer { get; set; }
        }
        public class ResponseElementsEntity
        {
            public string XAmzId2 { get; set; }
        }
    }
}
