/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public class InstrumentedMethodCall
    {
        public readonly MethodCall MethodCall;
        public readonly InstrumentedMethodInfo InstrumentedMethodInfo;

        public bool IsAsync => InstrumentedMethodInfo.IsAsync;
        public string RequestedMetricName => InstrumentedMethodInfo.RequestedMetricName;
        public int? RequestedTransactionNamePriority => InstrumentedMethodInfo.RequestedTransactionNamePriority;
        public bool StartWebTransaction => InstrumentedMethodInfo.StartWebTransaction;

        public InstrumentedMethodCall(MethodCall methodCall, InstrumentedMethodInfo instrumentedMethodInfo)
        {
            MethodCall = methodCall;
            InstrumentedMethodInfo = instrumentedMethodInfo;
        }
    }
}
