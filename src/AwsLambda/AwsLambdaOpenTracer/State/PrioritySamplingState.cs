using System.Threading;

namespace NewRelic.OpenTracing.AmazonLambda.State
{
    internal class PrioritySamplingState
    {

        private float _priority = 0.0f;
        private bool sampled = false;

        public bool Sampled { get => sampled; set => sampled = value; }

        public float Priority
        {
            get { return _priority; }
            set { Interlocked.Exchange(ref _priority, value); }
        }

        public void SetSampledAndGeneratePriority(bool computeSampled)
        {
            var priority = LambdaTracer.TracePriorityManager.Create() + (computeSampled ? 1.0f : 0.0f);
            Sampled = computeSampled;
            Priority = priority;
        }
    }
}
