// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.Transactions;

public interface IImmutableTransactionMetadata : ITransactionAttributeMetadata
{
    IEnumerable<string> CrossApplicationAlternatePathHashes { get; }
    string CrossApplicationReferrerTransactionGuid { get; }
    string CrossApplicationReferrerPathHash { get; }
    string CrossApplicationPathHash { get; }
    string CrossApplicationReferrerProcessId { get; }
    string CrossApplicationReferrerTripId { get; }
    float CrossApplicationResponseTimeInSeconds { get; }

    bool HasOutgoingTraceHeaders { get; }
    int? HttpResponseSubStatusCode { get; }
    string SyntheticsResourceId { get; }
    string SyntheticsJobId { get; }
    string SyntheticsMonitorId { get; }
    bool IsSynthetics { get; }
    bool HasCatResponseHeaders { get; }
}