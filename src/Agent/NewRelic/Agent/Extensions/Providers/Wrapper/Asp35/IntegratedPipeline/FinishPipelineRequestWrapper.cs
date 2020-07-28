using System.Web;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Providers.Wrapper.Asp35.Shared;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.Asp35.IntegratedPipeline
{
    public class FinishPipelineRequestWrapper : IWrapper
    {
        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "System.Web", typeName: "System.Web.HttpRuntime", methodName: "FinishPipelineRequest");
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            if (!HttpRuntime.UsingIntegratedPipeline)
                return Delegates.NoOp;

            var httpContext = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<HttpContext>(0);
            HttpContextActions.TransactionShutdown(agentWrapperApi, httpContext);

            var segment = agentWrapperApi.CastAsSegment(httpContext.Items[HttpContextActions.HttpContextSegmentKey] as ISegment);
            httpContext.Items[HttpContextActions.HttpContextSegmentKey] = null;
            httpContext.Items[HttpContextActions.HttpContextSegmentTypeKey] = null;
            segment.End();
            agentWrapperApi.CurrentTransaction.End();

            return Delegates.NoOp;
        }
    }
}
