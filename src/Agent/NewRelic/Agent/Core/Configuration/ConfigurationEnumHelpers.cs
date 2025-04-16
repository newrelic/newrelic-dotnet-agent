// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Config;

namespace NewRelic.Agent.Core.Configuration
{
    public static class ConfigurationEnumHelpers
    {
        public static RemoteParentSampledBehavior ToRemoteParentSampledBehavior(
            this RemoteParentSampledBehaviorType localConfigEnumValue)
        {
            switch (localConfigEnumValue)
            {
                case RemoteParentSampledBehaviorType.@default:
                    return RemoteParentSampledBehavior.Default;
                case RemoteParentSampledBehaviorType.alwaysOn:
                    return RemoteParentSampledBehavior.AlwaysOn;
                case RemoteParentSampledBehaviorType.alwaysOff:
                    return RemoteParentSampledBehavior.AlwaysOff;
                default:
                    throw new ArgumentOutOfRangeException(nameof(localConfigEnumValue), localConfigEnumValue, null);
            }
        }
    }
}
