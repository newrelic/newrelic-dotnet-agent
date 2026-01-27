// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Utilities;

public class BootIdResult
{
    public string BootId { get; }
    public bool IsValid { get; }

    public BootIdResult(string bootId, bool isValid)
    {
        BootId = bootId;
        IsValid = isValid;
    }

}
