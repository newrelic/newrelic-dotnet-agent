// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.JsonConverters
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class JsonArrayIndexAttribute : System.Attribute
    {
        public uint Index { get; set; }
    }
}
