// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NewRelic.Agent.Configuration
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RemoteParentSampledBehavior
    {
        [EnumMember(Value = "default")]
        Default,

        [EnumMember(Value = "always_on")]
        AlwaysOn,

        [EnumMember(Value = "always_off")]
        AlwaysOff
    }
}
