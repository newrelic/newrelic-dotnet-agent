// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Api.Agent
{
    internal class NoOpTransaction : ITransaction
    {
        private static ISpan _noOpSpan = new NoOpSpan();

        public ITransaction AddCustomAttribute(string key, object value)
        {
            return this;
        }

        public void InsertDistributedTraceHeaders<T>(T carrier, Action<T, string, string> setter)
        {
        }

        public void AcceptDistributedTraceHeaders<T>(T carrier, Func<T, string, IEnumerable<string>> getter, TransportType transportType)
        {
        }

        public ISpan CurrentSpan => _noOpSpan;

        public void SetUserId(string userid)
        {
        }

        public SegmentWrapper? RecordDatastoreSegment(string vendor, string model, string operation,
            string? commandText, string? host, string? portPathOrID, string? databaseName)
        {
            return null;
        }
    }
}
