/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;

namespace NewRelic.Agent.Core.JsonConverters
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class TimeSpanSerializesAsSecondsAttribute : System.Attribute { }

}
