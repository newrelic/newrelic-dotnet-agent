// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NewRelic.Agent.Configuration
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum SamplerType
    {
        [EnumMember(Value = "default")]
        Default,

        [EnumMember(Value = "always_on")]
        AlwaysOn,

        [EnumMember(Value = "always_off")]
        AlwaysOff,

        [EnumMember(Value = "trace_id_ratio_based")]
        TraceIdRatioBased
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum SamplerLevel
    {
        [EnumMember(Value = "root")]
        Root,
        [EnumMember(Value = "remote_parent_sampled")]
        RemoteParentSampled,
        [EnumMember(Value = "remote_parent_not_sampled")]
        RemoteParentNotSampled,
    }

}
