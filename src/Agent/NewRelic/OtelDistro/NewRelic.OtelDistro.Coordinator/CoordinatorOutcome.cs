// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.OtelDistro.Coordinator;

/// <summary>What a <see cref="RegistrationCoordinator.Register"/> call did.</summary>
public enum CoordinatorOutcome
{
    /// <summary>New definitions were sent to OTel native and native accepted at least one.</summary>
    Sent,

    /// <summary>Every definition in the batch was already sent earlier in this process; native was not called.</summary>
    AllPresent,

    /// <summary>Nothing was marked sent: bad input, native took none, or native threw.</summary>
    Failed
}
