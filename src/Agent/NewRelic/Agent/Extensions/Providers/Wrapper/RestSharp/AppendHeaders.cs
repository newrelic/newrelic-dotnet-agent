// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.RestSharp
{
    /// <summary>
    /// This instrumentation is used for CAT support on outbound RestClient requests.
    /// Data is added to the Http Headers to be read by the receiving agent.
    /// </summary>
    public class AppendHeaders : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            return new CanWrapResponse("NewRelic.Providers.Wrapper.RestSharp.AppendHeaders".Equals(instrumentedMethodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall,
            IAgent agent, ITransaction transaction)
        {
            var httpWebRequest = (HttpWebRequest)instrumentedMethodCall.MethodCall.MethodArguments[0];

            var setHeaders = new Action<HttpWebRequest, string, string>((carrier, key, value) =>
            {
                // 'Set' will replace an existing value
                httpWebRequest.Headers?.Set(key, value);
            });

            agent.CurrentTransaction.InsertDistributedTraceHeaders(httpWebRequest, setHeaders);

            return Delegates.NoOp;
        }
    }
}
