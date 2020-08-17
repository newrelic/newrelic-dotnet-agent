// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.HttpWebRequest
{
    public class SerializeHeadersWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "System", typeName: "System.Net.HttpWebRequest", methodName: "SerializeHeaders");
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var request = (System.Net.HttpWebRequest)instrumentedMethodCall.MethodCall.InvocationTarget;

            if (request == null)
            {
                throw new NullReferenceException(nameof(request));
            }

            if (request.Headers == null)
            {
                throw new NullReferenceException("request.Headers");
            }

            var setHeaders = new Action<System.Net.HttpWebRequest, string, string>((carrier, key, value) =>
            {
                carrier.Headers?.Set(key, value);
            });

            try
            {
                transaction.InsertDistributedTraceHeaders(request, setHeaders);
            }
            catch (Exception ex)
            {
                agent.HandleWrapperException(ex);
            }

            return Delegates.NoOp;
        }
    }
}
