// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Owin
{
    /// <summary>
    /// This instrumentation is for OWIN 2.
    /// If EndRequest gets inlined, this instrumentation will ensure the transaction is ended for vast majority of cases.
    /// The only case this wont cover is if there is an exception in StartProcessingRequest 
    /// prior to creating the OwinHttpListenerContext
    /// </summary>
    public class OwinHttpListenerContextEnd : IWrapper
    {
        private const string AssemblyName = "Microsoft.Owin.Host.HttpListener";
        private const string TypeName = "Microsoft.Owin.Host.HttpListener.RequestProcessing.OwinHttpListenerContext";
        private const string MethodName = "End";
        private const int SupportedMajorVersion = 2;

        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: AssemblyName, typeName: TypeName, methodName: MethodName);

            var version = method.Type.Assembly.GetName().Version;
            canWrap = canWrap && (version.Major == SupportedMajorVersion);

            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            var owinHttpListenerContext = instrumentedMethodCall.MethodCall.InvocationTarget;
            var owinTransaction = OwinTransactionContext.ExtractTransactionFromContext(owinHttpListenerContext);

            if (owinTransaction == null)
            {
                return Delegates.NoOp;
            }

            return Delegates.GetDelegateFor(() =>
            {
                owinTransaction.End();
            });
        }
    }
}
