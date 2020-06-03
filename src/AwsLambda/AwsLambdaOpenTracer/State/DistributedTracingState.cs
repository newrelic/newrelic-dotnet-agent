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
