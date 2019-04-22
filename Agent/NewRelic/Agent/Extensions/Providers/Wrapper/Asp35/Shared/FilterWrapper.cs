using System;
using System.IO;
using System.Web;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Asp35.Shared
{
	public class FilterWrapper : IWrapper
	{
		private const String BrowerAgentInjectedKey = "NewRelic.BrowerAgentInjected";

		public bool IsTransactionRequired => false;

		private static class Statics
		{
			[NotNull]
			public static readonly Func<HttpWriter, Boolean> IgnoringFurtherWrites = VisibilityBypasser.Instance.GeneratePropertyAccessor<HttpWriter, Boolean>("IgnoringFurtherWrites");
		}

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "System.Web", typeName: "System.Web.HttpWriter", methodNames: new [] { "Filter", "FilterIntegrated" });
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
			var httpContext = HttpContext.Current;
			// we have seen httpContext == null in the wild, so don't throw an exception
			if (httpContext == null)
				return Delegates.NoOp;

			//have we already added our filter?  If so, we are done.
			if (httpContext.Items.Contains(BrowerAgentInjectedKey))
				return Delegates.NoOp;

			if (httpContext.Response.StatusCode >= 400)
				return Delegates.NoOp;

			var httpWriter = (HttpWriter)instrumentedMethodCall.MethodCall.InvocationTarget;
			if (httpWriter == null)
				throw new NullReferenceException("httpWriter");

			if (Statics.IgnoringFurtherWrites(httpWriter))
				return Delegates.NoOp;

			//add our filter and add a key to httpContext.Items to reflect this. 
			//   (the key is used above to insure we only add our filter once).
			var newFilter = TryGetStreamInjector(agent, httpContext);
			if (newFilter != null)
			{
				httpContext.Response.Filter = newFilter;
				httpContext.Items[BrowerAgentInjectedKey] = true;
			}

			return Delegates.NoOp;
		}

		[CanBeNull]
		private static Stream TryGetStreamInjector([NotNull] IAgent agent, [NotNull] HttpContext httpContext)
		{
			var currentFilter = httpContext.Response.Filter;
			var contentEncoding = httpContext.Response.ContentEncoding;
			var contentType = httpContext.Response.ContentType;
			var requestPath = httpContext.Request.Path;

			// NOTE: We need to be very careful if we decide to move where TryGetStreamInjector is called from. The agent assumes that this call will happen fairly late in the pipeline as it has a side-effect of freezing the transaction name and capturing all of the currently recorded transaction attributes.
			return agent.TryGetStreamInjector(currentFilter, contentEncoding, contentType, requestPath);
		}
	}
}
