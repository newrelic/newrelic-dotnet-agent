// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Mock.Amazon.Lambda.KinesisFirehoseEvents
{
    /// <summary>
    ///  https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.KinesisFirehoseEvents/KinesisFirehoseEvent.cs
    /// </summary>
    public class KinesisFirehoseEvent
    {
        public string DeliveryStreamArn { get; set; }
        public string Region { get; set; }
        public List<FirehoseRecord> Records { get; set; }

        public class FirehoseRecord
        {
        }
    }
}
