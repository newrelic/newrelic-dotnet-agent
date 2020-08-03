// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Owin
{
    /// <summary>
    /// This instrumentation is for OWIN 2
    /// The transaction is stored in the Environment so we can end the transaction on a new thread
    /// that is spawned outside of the web api async flow. We don't use AsyncLocal & clean-up in StartProcessingRequest
    /// because StartProcessingRequest spins up new threads for the next request and it would flow to those.
    /// </summary>
    public class PopulateServerKeysWrapper : IWrapper
    {
        private const string TypeName = "Microsoft.Owin.Host.HttpListener.OwinHttpListener";
        private const string MethodName = "PopulateServerKeys";
        private const int SupportedMajorVersion = 2;

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;

            var canWrap = method.MatchesAny(assemblyName: "Microsoft.Owin.Host.HttpListener", typeName: TypeName, methodName: MethodName);
            var version = method.Type.Assembly.GetName().Version;

            canWrap = canWrap && (version.Major == SupportedMajorVersion);

            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            var callEnvironment = instrumentedMethodCall.MethodCall.MethodArguments[0];
            OwinTransactionContext.SetTransactionOnEnvironment(callEnvironment, transaction);

            return Delegates.NoOp;
        }
    }
}
