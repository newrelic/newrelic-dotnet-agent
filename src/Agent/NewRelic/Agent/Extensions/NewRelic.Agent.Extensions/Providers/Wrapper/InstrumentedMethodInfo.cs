/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    /// <summary>
    /// The immutable details about an instrumented method.
    /// </summary>
    public class InstrumentedMethodInfo
    {
        private readonly long _functionId;
        public readonly Method Method;
        public readonly string RequestedWrapperName;
        public readonly bool IsAsync;
        public readonly string RequestedMetricName;
        public readonly TransactionNamePriority? RequestedTransactionNamePriority;
        public readonly bool StartWebTransaction;

        public InstrumentedMethodInfo(long functionId, Method method, string requestedWrapperName, bool isAsync, string requestedMetricName, TransactionNamePriority? requestedTransactionNamePriority, bool startWebTransaction)
        {
            Method = method;
            RequestedWrapperName = requestedWrapperName;
            _functionId = functionId;
            IsAsync = isAsync;
            RequestedMetricName = requestedMetricName;
            RequestedTransactionNamePriority = requestedTransactionNamePriority;
            StartWebTransaction = startWebTransaction;
        }

        public override int GetHashCode()
        {
            return _functionId.GetHashCode();
        }

        public override bool Equals(object other)
        {
            if (!(other is InstrumentedMethodInfo))
                return false;

            var otherMethod = (InstrumentedMethodInfo)other;
            return _functionId == otherMethod._functionId;
        }

        public override string ToString()
        {
            return $"Method: {Method}, RequestedWrapperName: {RequestedWrapperName}, IsAsync: {IsAsync}, RequestedMetricName: {RequestedMetricName}, RequestedTransactionNamePriority: {RequestedTransactionNamePriority}, StartWebTransaction: {StartWebTransaction}";
        }
    }
}
