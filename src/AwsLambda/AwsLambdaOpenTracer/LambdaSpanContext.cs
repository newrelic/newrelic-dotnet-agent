// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.OpenTracing.AmazonLambda.State;

namespace NewRelic.OpenTracing.AmazonLambda
{
    internal class LambdaSpanContext : LambdaBaseContext
    {
        private readonly LambdaSpan _span;

        public LambdaSpanContext(LambdaSpan span)
        {
            _span = span;
        }

        public LambdaSpan GetSpan()
        {
            return _span;
        }
    }
}
