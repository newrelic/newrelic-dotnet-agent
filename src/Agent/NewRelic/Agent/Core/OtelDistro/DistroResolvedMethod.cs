// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>How <see cref="DistroMethodResolver"/> identified the method that fired for a distro integration.</summary>
public enum DistroResolutionOutcome
{
    /// <summary>Exactly one method matched.</summary>
    Resolved,

    /// <summary>Several methods matched and they agree on async-ness; the first candidate's identity is used.</summary>
    AmbiguousAgreeing,

    /// <summary>Several methods matched and they disagree on async-ness; the call is treated as not async.</summary>
    AmbiguousDisagreeing,

    /// <summary>No method matched (or resolution failed); the call is treated as not async.</summary>
    NotFound
}

/// <summary>
/// The identity of the method that fired for one distro integration instantiation: whether it is async, the function id
/// to use, the real declaring type and the argument signature. Unresolved results carry the site's values.
/// </summary>
public sealed class DistroResolvedMethod
{
    public DistroResolutionOutcome Outcome { get; }

    public bool IsResolved => Outcome == DistroResolutionOutcome.Resolved || Outcome == DistroResolutionOutcome.AmbiguousAgreeing;

    public bool IsAsync { get; }

    public ulong FunctionId { get; }

    public Type DeclaringType { get; }

    public string ArgumentSignature { get; }

    public DistroResolvedMethod(DistroResolutionOutcome outcome, bool isAsync, ulong functionId, Type declaringType, string argumentSignature)
    {
        Outcome = outcome;
        IsAsync = isAsync;
        FunctionId = functionId;
        DeclaringType = declaringType;
        ArgumentSignature = argumentSignature;
    }
}
