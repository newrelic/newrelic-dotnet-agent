// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Config;

namespace NewRelic.Agent.Core.Configuration;

public static class ConfigurationEnumHelpers
{
    public static SamplerType ToRemoteParentSamplerType(this RemoteParentSampledBehaviorType localConfigEnumValue)
    {
        switch (localConfigEnumValue)
        {
            case RemoteParentSampledBehaviorType.@default:
            case RemoteParentSampledBehaviorType.adaptive:
                return SamplerType.Adaptive;
            case RemoteParentSampledBehaviorType.alwaysOn:
                return SamplerType.AlwaysOn;
            case RemoteParentSampledBehaviorType.alwaysOff:
                return SamplerType.AlwaysOff;
            case RemoteParentSampledBehaviorType.traceIdRatioBased:
                return SamplerType.TraceIdRatioBased;
            default:
                throw new ArgumentOutOfRangeException(nameof(localConfigEnumValue), localConfigEnumValue, null);
        }
    }

    public static SamplerType ToRemoteParentSamplerType(this string remoteParentSampledBehavior)
    {
        switch (remoteParentSampledBehavior.ToLower())
        {
            case "default":
            case "adaptive":
                return SamplerType.Adaptive;
            case "alwayson":
                return SamplerType.AlwaysOn;
            case "alwaysoff":
                return SamplerType.AlwaysOff;
            case "traceidratiobased":
                return SamplerType.TraceIdRatioBased;
            default:
                return SamplerType.Adaptive; // default to adaptive if unrecognized value
        }
    }


    public static object ToConfigurationSamplerTypeInstance(this SamplerType samplerType)
    {
        switch (samplerType)
        {
            case SamplerType.Adaptive:
                return new AdaptiveSamplerType();
            case SamplerType.AlwaysOn:
                return new AlwaysOnSamplerType();
            case SamplerType.AlwaysOff:
                return new AlwaysOffSamplerType();
            case SamplerType.TraceIdRatioBased:
                return new TraceIdRatioBasedSamplerType();
            default:
                throw new ArgumentOutOfRangeException(nameof(samplerType), samplerType, null);
        }
    }

    public static string ToSamplerTypeString(this SamplerType samplerType)
    {
        switch (samplerType)
        {
            case SamplerType.Adaptive:
                return "adaptive";
            case SamplerType.AlwaysOn:
                return "always_on";
            case SamplerType.AlwaysOff:
                return "always_off";
            case SamplerType.TraceIdRatioBased:
                return "trace_id_ratio_based";
            default:
                throw new ArgumentOutOfRangeException(nameof(samplerType), samplerType, null);
        }
    }

    public static string ToSamplerLevelString(this SamplerLevel samplerLevel)
    {
        switch (samplerLevel)
        {
            case SamplerLevel.Root:
                return "root";
            case SamplerLevel.RemoteParentSampled:
                return "remote_parent_sampled";
            case SamplerLevel.RemoteParentNotSampled:
                return "remote_parent_not_sampled";
            default:
                throw new ArgumentOutOfRangeException(nameof(samplerLevel), samplerLevel, null);
        }
    }
}