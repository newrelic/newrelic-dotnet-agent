// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Utilization;

namespace NewRelic.Agent.Core.Utilities
{
    public interface ISystemInfo
    {
        ulong GetTotalPhysicalMemoryBytes();
        int GetTotalLogicalProcessors();
        BootIdResult GetBootId();
    }
}
