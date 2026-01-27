// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Utilities;

public interface ISystemInfo
{
    ulong? GetTotalPhysicalMemoryBytes();
    int? GetTotalLogicalProcessors();
    BootIdResult GetBootId();
}
