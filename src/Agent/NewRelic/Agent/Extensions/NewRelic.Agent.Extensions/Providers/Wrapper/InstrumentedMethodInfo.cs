using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    /// <summary>
    /// The immutable details about an instrumented method.
    /// </summary>
    public class InstrumentedMethodInfo
    {
        private readonly long _functionId;
        public readonly Method Method;
        [CanBeNull]
        public readonly String RequestedWrapperName;
        [NotNull]
        public readonly bool IsAsync;
        [CanBeNull]
        public readonly string RequestedMetricName;
        [CanBeNull]
        public readonly int? RequestedTransactionNamePriority;
        public readonly bool StartWebTransaction;

        public InstrumentedMethodInfo(long functionId, Method method, [CanBeNull] String requestedWrapperName, bool isAsync, [CanBeNull] string requestedMetricName, [CanBeNull] int? requestedTransactionNamePriority, bool startWebTransaction)
        {
            Method = method;
            RequestedWrapperName = requestedWrapperName;
            _functionId = functionId;
            IsAsync = isAsync;
            RequestedMetricName = requestedMetricName;
            RequestedTransactionNamePriority = requestedTransactionNamePriority;
            StartWebTransaction = startWebTransaction;
        }

        public override Int32 GetHashCode()
        {
            return _functionId.GetHashCode();
        }

        public override Boolean Equals(Object other)
        {
            if (!(other is InstrumentedMethodInfo))
                return false;

            var otherMethod = (InstrumentedMethodInfo)other;
            return _functionId == otherMethod._functionId;
        }
    }
}
