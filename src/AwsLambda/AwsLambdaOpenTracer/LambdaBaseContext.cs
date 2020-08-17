// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using OpenTracing;
using System;
using System.Collections.Generic;

namespace NewRelic.OpenTracing.AmazonLambda
{
    internal abstract class LambdaBaseContext : ISpanContext
    {
        private readonly IEnumerable<KeyValuePair<string, string>> _baggageItems;

        public LambdaBaseContext()
        {
            _baggageItems = new Dictionary<string, string>();
        }

        public string TraceId => throw new NotImplementedException();

        public string SpanId => throw new NotImplementedException();

        public IEnumerable<KeyValuePair<string, string>> GetBaggageItems()
        {
            return _baggageItems;
        }
    }
}
