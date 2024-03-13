// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Api.Agent
{
    public class SegmentWrapper : IDisposable
    {
        private volatile dynamic _segment;

        public static SegmentWrapper GetDatastoreWrapper(dynamic transaction,
            string vendor, string model, string operation,
            string? commandText, string? host, string? portPathOrID, string? databaseName)
        {
            return new SegmentWrapper(transaction.StartDatastoreSegment(vendor, model, operation,
                commandText, host, portPathOrID, databaseName));
        }

        private SegmentWrapper(dynamic segment)
        {
            _segment = segment;
        }

        public void Dispose()
        {
            _segment.End();
        }
    }
}
