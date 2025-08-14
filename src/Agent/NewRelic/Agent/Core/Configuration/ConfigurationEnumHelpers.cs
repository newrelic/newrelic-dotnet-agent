// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Config;

namespace NewRelic.Agent.Core.Configuration
{
    public static class ConfigurationEnumHelpers
    {
        public static SamplerType ToRemoteParentSampledBehavior(
            this RemoteParentSampledBehaviorType localConfigEnumValue)
        {
            switch (localConfigEnumValue)
            {
                case RemoteParentSampledBehaviorType.@default:
                    return SamplerType.Default;
                case RemoteParentSampledBehaviorType.alwaysOn:
                    return SamplerType.AlwaysOn;
                case RemoteParentSampledBehaviorType.alwaysOff:
                    return SamplerType.AlwaysOff;
                default:
                    throw new ArgumentOutOfRangeException(nameof(localConfigEnumValue), localConfigEnumValue, null);
            }
        }

        public static SamplerType ToRemoteParentSampledBehavior(this string remoteParentSampledBehavior)
        {
            switch (remoteParentSampledBehavior.ToLower())
            {
                case "default":
                    return SamplerType.Default;
                case "alwayson":
                    return SamplerType.AlwaysOn;
                case "alwaysoff":
                    return SamplerType.AlwaysOff;
                case "traceidratiobased":
                    return SamplerType.TraceIdRatioBased;
                default:
                    return SamplerType.Default;
            }
        }

        public static RemoteParentSampledBehaviorType ToRemoteParentSampledBehaviorType(
            this SamplerType remoteParentSampledBehavior)
        {
            switch (remoteParentSampledBehavior)
            {
                case SamplerType.Default:
                    return RemoteParentSampledBehaviorType.@default;
                case SamplerType.AlwaysOn:
                    return RemoteParentSampledBehaviorType.alwaysOn;
                case SamplerType.AlwaysOff:
                    return RemoteParentSampledBehaviorType.alwaysOff;
                case SamplerType.TraceIdRatioBased:
                    return RemoteParentSampledBehaviorType.traceIdRatioBased;
                default:
                    throw new ArgumentOutOfRangeException(nameof(remoteParentSampledBehavior), remoteParentSampledBehavior, null);
            }
        }

        public static object ToConfiguredSamplerType(this SamplerType samplerType)
        {
            switch (samplerType)
            {
                case SamplerType.Default:
                    return new DefaultSamplerType();
                case SamplerType.AlwaysOn:
                    return new AlwaysOnSamplerType();
                case SamplerType.AlwaysOff:
                    return new AlwaysOffSamplerType();
                case SamplerType.TraceIdRatioBased:
                    return new TraceIdRatioSamplerType();
                default:
                    throw new ArgumentOutOfRangeException(nameof(samplerType), samplerType, null);
            }
        }
    }
}
