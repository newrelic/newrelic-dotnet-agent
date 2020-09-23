
// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.HttpClient
{
    public class NoOp : IWrapper
    {
        public bool IsTransactionRequired => true;

        private const string AssemblyName = "System.Net.Http";
        private const string HttpClientTypeName = "System.Net.Http.HttpClient";
        private const string SocketsHttpHandlerTypeName = "System.Net.Http.SocketsHttpHandler";
        private const string SendAsyncMethodName = "SendAsync";
        private const string SendMethodName = "Send";
        private const int DotNet5AssemblyVersionMajor = 5;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;

            var version = method.Type.Assembly.GetName().Version;

            if (version.Major >= DotNet5AssemblyVersionMajor && method.MatchesAny(assemblyName: AssemblyName, typeName: HttpClientTypeName, methodName: SendAsyncMethodName))
            {
                return new CanWrapResponse(true);
            }
            else if (version.Major < DotNet5AssemblyVersionMajor && method.MatchesAny(assemblyName: AssemblyName, typeName: SocketsHttpHandlerTypeName, methodName: SendAsyncMethodName))
            {
                return new CanWrapResponse(true);
            }
            else if (version.Major < DotNet5AssemblyVersionMajor && method.MatchesAny(assemblyName: AssemblyName, typeName: SocketsHttpHandlerTypeName, methodName: SendMethodName))
            {
                return new CanWrapResponse(true);
            }

            return new CanWrapResponse(false);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            return Delegates.NoOp;
        }
    }
}
