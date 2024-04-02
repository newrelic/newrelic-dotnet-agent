// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Mock.Amazon.Lambda.CloudWatchEvents.ScheduledEvents
{
    // https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.CloudWatchEvents/ScheduledEvents/ScheduledEvent.cs
    // https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.CloudWatchEvents/CloudWatchEvent.cs
    public class ScheduledEvent
    {
        public string Account { get; set; }
        public string Id { get; set; }
        public string Region { get; set; }
        public List<string> Resources { get; set; }
        public DateTime Time { get; set; }
    }
}
