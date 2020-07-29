using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Owin
{
    /// <summary>
    /// This instrumentation is for OWIN 2
    /// EndRequest is the ideal place to end transactions (hit by every code path) but appears to occasionally be inlined.
    /// OwinHttpListenerContext.End(Exception) is also instrumented as a fall-back in case EndRequest gets inlined.
    /// </summary>
    public class EndRequestWrapper : IWrapper
    {
        private const string AssemblyName = "Microsoft.Owin.Host.HttpListener";
        private const string TypeName = "Microsoft.Owin.Host.HttpListener.OwinHttpListener";
        private const string MethodName = "EndRequest";

        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: AssemblyName, typeName: TypeName, methodName: MethodName);
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            var owinHttpListenerContext = instrumentedMethodCall.MethodCall.MethodArguments[0];
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
