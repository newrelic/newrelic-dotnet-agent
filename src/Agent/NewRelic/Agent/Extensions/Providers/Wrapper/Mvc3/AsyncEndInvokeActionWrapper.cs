﻿using System;
using System.Web;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Mvc3
{
	public class AsyncEndInvokeActionWrapper : IWrapper
	{
		public bool IsTransactionRequired => false;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "System.Web.Mvc", typeName: "System.Web.Mvc.Async.AsyncControllerActionInvoker", methodName: "EndInvokeAction");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			var httpContext = HttpContext.Current;
			if(httpContext == null)
				throw new NullReferenceException("httpContext");

			var segment = agentWrapperApi.CastAsSegment(httpContext.Items[AsyncBeginInvokeActionWrapper.HttpContextSegmentKey]);
			httpContext.Items[AsyncBeginInvokeActionWrapper.HttpContextSegmentKey] = null;
			return Delegates.GetDelegateFor(segment);
		}
	}
}
