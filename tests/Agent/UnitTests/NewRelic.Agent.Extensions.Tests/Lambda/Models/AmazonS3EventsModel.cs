// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Mock.Amazon.Lambda.S3Events
{
    public class S3Event
    {
        public List<S3EventRecord> Records { get; set; }
    }

    public class S3EventRecord
    {
        public string AwsRegion { get; set; }
        public string EventTime { get; set; }
        public string EventName { get; set; }
        public S3Entity S3 { get; set; }
        public S3ResponseElements ResponseElements { get; set; }
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
        public string Size { get; set; }
        public string Sequencer { get; set; }
    }
    public class S3ResponseElements
    {
        public string XAmzId2 { get; set; }
    }
}
