// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;
using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.DistributedTracing
{
    public interface ITracingState
    {
        DistributedTracingParentType Type { get; }

        string AppId { get; }

        string AccountId { get; }

        TransportType TransportType { get; }

        string Guid { get; }

        string ParentId { get; }

        DateTime Timestamp { get; }

        TimeSpan TransportDuration { get; }

        string TraceId { get; }

        string TransactionId { get; }

        bool? Sampled { get; }

        float? Priority { get; }

        bool NewRelicPayloadWasAccepted { get; }
        bool TraceContextWasAccepted { get; }
        bool HasDataForParentAttributes { get; }
        bool HasDataForAttributes { get; }

        List<IngestErrorType> IngestErrors { get; }

        List<string> VendorStateEntries { get; }
    }
}
