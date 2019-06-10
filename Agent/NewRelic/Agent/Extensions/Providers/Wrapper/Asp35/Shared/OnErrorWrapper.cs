
using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.Asp35.Shared
{
	public class OnErrorWrapper : IWrapper
	{
		public const string WrapperName = "Asp35.OnErrorTracer";

		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var canWrap = methodInfo.RequestedWrapperName.Equals(WrapperName, StringComparison.OrdinalIgnoreCase);
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
			var exception = instrumentedMethodCall.MethodCall.MethodArguments.ExtractAs<Exception>(0);
			if (exception == null)
				return Delegates.NoOp;

			transaction.NoticeError(exception);

			return Delegates.NoOp;
		}
	}
}