// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace NewRelic.OpenTracing.AmazonLambda.Events
{
    internal abstract class Event
    {
        public abstract IDictionary<string, object> Intrinsics { get; }

        public abstract IDictionary<string, object> UserAttributes { get; }

        public abstract IDictionary<string, object> AgentAttributes { get; }

        // Print an event according to the required data format, which is an array of 3 hashes representing intrinsics,
        // user attributes, and agent attributes.
        public override string ToString()
        {
            return ToJsonString();
        }

        public string ToJsonString()
        {
            return JsonConvert.SerializeObject(new IDictionary<string, object>[] { Intrinsics, UserAttributes, AgentAttributes });
        }

    }
}
