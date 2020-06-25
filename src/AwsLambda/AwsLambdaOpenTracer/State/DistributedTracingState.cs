/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Core.DistributedTracing;

namespace NewRelic.OpenTracing.AmazonLambda.State
{
    internal class DistributedTracingState
    {
        public DistributedTracePayload InboundPayload { get; private set; }

        public double TransportDurationInMillis { get; private set; }

        public void SetInboundDistributedTracePayload(DistributedTracePayload payload)
        {
            InboundPayload = payload;
        }

        public void SetTransportDurationInMillis(double transportDurationInMillis)
        {
            TransportDurationInMillis = transportDurationInMillis;
        }
    }
}
