using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Owin
{
    /// <summary>
    /// This instrumentation is used for OWIN 2
    /// </summary>
    public class StartProcessingRequestWrapper : IWrapper
    {
        private const string TypeName = "Microsoft.Owin.Host.HttpListener.OwinHttpListener";
        private const string MethodName = "StartProcessingRequest";

        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "Microsoft.Owin.Host.HttpListener", typeName: TypeName, methodName: MethodName);
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Custom, $"{TypeName}/{MethodName}");
            var segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, TypeName, MethodName);

            return Delegates.GetDelegateFor(() =>
            {
                transaction.Detach();
                segment.End();
            });
        }
    }
}
