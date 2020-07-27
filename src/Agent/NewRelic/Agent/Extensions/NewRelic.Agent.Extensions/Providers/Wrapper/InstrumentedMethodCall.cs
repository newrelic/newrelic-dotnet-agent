using JetBrains.Annotations;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public class InstrumentedMethodCall
    {
        public readonly MethodCall MethodCall;
        public readonly InstrumentedMethodInfo InstrumentedMethodInfo;

        public bool IsAsync => InstrumentedMethodInfo.IsAsync;
        [CanBeNull]
        public string RequestedMetricName => InstrumentedMethodInfo.RequestedMetricName;
        [CanBeNull]
        public int? RequestedTransactionNamePriority => InstrumentedMethodInfo.RequestedTransactionNamePriority;
        public bool StartWebTransaction => InstrumentedMethodInfo.StartWebTransaction;

        public InstrumentedMethodCall(MethodCall methodCall, [NotNull] InstrumentedMethodInfo instrumentedMethodInfo)
        {
            MethodCall = methodCall;
            InstrumentedMethodInfo = instrumentedMethodInfo;
        }
    }
}
