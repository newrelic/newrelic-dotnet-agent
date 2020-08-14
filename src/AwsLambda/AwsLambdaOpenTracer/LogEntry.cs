// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.OpenTracing.AmazonLambda
{
    internal class LogEntry
    {
        public LogEntry(DateTimeOffset timestamp, object value)
        {
            Timestamp = timestamp;
            Value = value;
        }

        public DateTimeOffset Timestamp { get; }

        public object Value { get; }
    }
}
