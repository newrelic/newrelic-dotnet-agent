// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>What a registrar did with a registration request.</summary>
public enum DistroSiteRegistrationOutcome
{
    /// <summary>At least one requested site was sent to OTel.</summary>
    Sent,

    /// <summary>Every requested site was already registered in this process; OTel was not called.</summary>
    AllPresent,

    /// <summary>The registrar was reached but registration failed; <see cref="DistroSiteRegistration.Failure"/> says why.</summary>
    Failed,

    /// <summary>The registrar could not be used (for example, the coordinator is gone); <see cref="DistroSiteRegistration.Failure"/> says why.</summary>
    Unavailable
}

/// <summary>The result of <see cref="IDistroSiteRegistrar.Register"/>.</summary>
public sealed class DistroSiteRegistration
{
    public DistroSiteRegistration(DistroSiteRegistrationOutcome outcome, int requested, int sent, string failure)
    {
        Outcome = outcome;
        Requested = requested;
        Sent = sent;
        Failure = failure;
    }

    public DistroSiteRegistrationOutcome Outcome { get; }

    /// <summary>How many sites were asked for.</summary>
    public int Requested { get; }

    /// <summary>How many of them were sent to OTel by this call (the rest were already registered).</summary>
    public int Sent { get; }

    /// <summary>The reason for <see cref="DistroSiteRegistrationOutcome.Failed"/> or <see cref="DistroSiteRegistrationOutcome.Unavailable"/>; otherwise null.</summary>
    public string Failure { get; }
}
