using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.Asp35.Shared
{
	public class OnErrorWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "System.Web", typeName: "System.Web.HttpApplication", methodName: "RecordError");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			var exception = instrumentedMethodCall.MethodCall.MethodArguments.ExtractAs<Exception>(0);
			if (exception == null)
				return Delegates.NoOp;

			transactionWrapperApi.NoticeError(exception);

			return Delegates.NoOp;
		}
	}
}
