// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Core.JsonConverters.LambdaPayloads
{

    public class SnsMessage
    {
        public IDictionary<string, MessageAttribute> MessageAttributes { get; set; }
    }

    public class MessageAttribute
    {
        public string Type { get; set; }
        public string Value { get; set; }
    }

}
