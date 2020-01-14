using System;
using System.Web.UI;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Asp35.Shared
{
	public class AspPagesTransactionNameWrapper : IWrapper
	{
		public const string WrapperName = "Asp35.AspPagesTransactionNameTracer";

		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var canWrap = methodInfo.RequestedWrapperName.Equals(WrapperName, StringComparison.OrdinalIgnoreCase);
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
			var page = instrumentedMethodCall.MethodCall.InvocationTarget as Page;
			if (page == null)
				return Delegates.NoOp;

			var pagePath = page.AppRelativeVirtualPath;
			if (pagePath == null)
				return Delegates.NoOp;

			if (pagePath.StartsWith("~/"))
				pagePath = pagePath.Substring(2);

			pagePath = pagePath.ToLower();

			transaction.SetWebTransactionName(WebTransactionType.ASP, pagePath, TransactionNamePriority.FrameworkHigh);
			var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, pagePath);

			return Delegates.GetDelegateFor(segment);
		}
	}
}