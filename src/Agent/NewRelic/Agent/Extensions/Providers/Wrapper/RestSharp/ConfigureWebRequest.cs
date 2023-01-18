// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.RestSharp
{
    /// <summary>
    /// This instrumentation is used for CAT/DT support on outbound RestClient requests.
    /// Data is added to the Http Headers to be read by the receiving agent.
    /// </summary>
    public class ConfigureWebRequest : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            return new CanWrapResponse("NewRelic.Providers.Wrapper.RestSharp.ConfigureWebRequest".Equals(instrumentedMethodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall,
            IAgent agent, ITransaction transaction)
        {
            return Delegates.GetDelegateFor<HttpWebRequest>(
                    onSuccess: response =>
                    {
                        var setHeaders = new Action<HttpWebRequest, string, string>((carrier, key, value) =>
                        {
                            // 'Set' will replace an existing value
                            response.Headers?.Set(key, value);
                        });

                        agent.CurrentTransaction.InsertDistributedTraceHeaders(response, setHeaders);
                    },
                    onFailure: exception =>
                    {
                        // log some kind of error message?
                    });
        }
    }
}
