using System;
using System.Web;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.WebOptimization
{
	public class StaticResourceNamingWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "System.Web.Optimization", typeName: "System.Web.Optimization.BundleHandler", methodName: "ProcessRequest");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			var httpContext = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<HttpContext>(0);
			var assetName = httpContext.Request.Path.TrimStart('/');

			transactionWrapperApi.SetWebTransactionName(WebTransactionType.ASP, assetName, TransactionNamePriority.FrameworkHigh);
			var segment = transactionWrapperApi.StartTransactionSegment(instrumentedMethodCall.MethodCall, assetName);
			
			if (segment == null)
				return Delegates.NoOp;

			return Delegates.GetDelegateFor(segment);
		}
	}
}
