// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.OtelDistro.Coordinator;

/// <summary>Plain, serializable result of a registration call, safe to return across an AppDomain boundary.</summary>
[Serializable]
public sealed class CoordinatorResult
{
    /// <summary>Creates a result.</summary>
    public CoordinatorResult(CoordinatorOutcome outcome, int requested, int sent, int nativeTaken, string id, string failure)
    {
        Outcome = outcome;
        Requested = requested;
        Sent = sent;
        NativeTaken = nativeTaken;
        Id = id;
        Failure = failure;
    }

    /// <summary>What the call did.</summary>
    public CoordinatorOutcome Outcome { get; }

    /// <summary>Number of definitions in the caller batch (duplicates included).</summary>
    public int Requested { get; }

    /// <summary>Number of distinct, previously unsent definitions handed to native.</summary>
    public int Sent { get; }

    /// <summary>The count native reported taking.</summary>
    public int NativeTaken { get; }

    /// <summary>The native registration id used for this call, or null when native was not called.</summary>
    public string Id { get; }

    /// <summary>Why the call failed, or null.</summary>
    public string Failure { get; }
}
