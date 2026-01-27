// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Api.Agent;

internal class NoOpAgent : IAgent
{
    private static ITransaction _noOpTransaction = new NoOpTransaction();
    private static ITraceMetadata _noOpTraceMetadata = new NoOpTraceMetadata();

    public ITransaction CurrentTransaction => _noOpTransaction;

    public ISpan CurrentSpan => _noOpTransaction.CurrentSpan;

    ITraceMetadata IAgent.TraceMetadata => _noOpTraceMetadata;

    public Dictionary<string, string> GetLinkingMetadata() => new Dictionary<string, string>();
}