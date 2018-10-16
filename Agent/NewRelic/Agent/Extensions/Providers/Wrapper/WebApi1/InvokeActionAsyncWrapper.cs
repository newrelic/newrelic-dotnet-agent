using System;
using System.Configuration;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.WebApi1
{
	public class AsyncApiControllerActionInvoker : IWrapper
	{
		private const String DotNet40BugMessage = "WebAPI v1 is not supported on .NET 4.0 unless you opt in to a .NET Framework bug fix. For details see: https://docs.newrelic.com/docs/agents/net-agent/troubleshooting/missing-async-metrics";

		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;

			var version = method.Type.Assembly.GetName().Version;
			if (version == null)
				return new CanWrapResponse(false);

			var canWrap = method.MatchesAny(assemblyName: "System.Web.Http", typeName: "System.Web.Http.Controllers.ApiControllerActionInvoker", methodName: "InvokeActionAsync") &&
				version.Major == 4; // WebApi v1 == System.Web.Http v4

			if (canWrap)
			{
				return TaskFriendlySyncContextValidator.CanWrapAsyncMethod("System.Web.Http", "System.Web.Http.Controllers.ApiControllerActionInvoker", method.MethodName);
			}

			return new CanWrapResponse(false);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			var httpActionContext = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<HttpActionContext>(0);
			var controllerName = TryGetControllerName(httpActionContext) ?? "Unknown Controller";
			var actionName = TryGetActionName(httpActionContext) ?? "Unknown Action";

			var transactionName = String.Format("{0}/{1}", controllerName, actionName);
			transactionWrapperApi.SetWebTransactionName(WebTransactionType.WebAPI, transactionName, TransactionNamePriority.FrameworkHigh);
			
			var segment = transactionWrapperApi.StartMethodSegment(instrumentedMethodCall.MethodCall, controllerName, actionName);

			return Delegates.GetDelegateFor<Task<HttpResponseMessage>>(
				onFailure: segment.End,
				onSuccess: task =>
				{
					segment.RemoveSegmentFromCallStack();

					if (task == null)
						return;

					var context = SynchronizationContext.Current;
					if (context != null)
					{
						task.ContinueWith(_ => agentWrapperApi.HandleExceptions(segment.End), 
							TaskScheduler.FromCurrentSynchronizationContext());
					}
					else
					{
						task.ContinueWith(_ => agentWrapperApi.HandleExceptions(segment.End), 
							TaskContinuationOptions.ExecuteSynchronously);
					}
				});
		}

		[CanBeNull]
		private static String TryGetControllerName([NotNull] HttpActionContext httpActionContext)
		{
			var controllerContext = httpActionContext.ControllerContext;
			if (controllerContext == null)
				return null;

			var controllerDescriptor = controllerContext.ControllerDescriptor;
			if (controllerDescriptor == null)
				return null;

			return controllerDescriptor.ControllerName;
		}

		[CanBeNull]
		private static String TryGetActionName([NotNull] HttpActionContext httpActionContext)
		{
			var actionDescriptor = httpActionContext.ActionDescriptor;
			if (actionDescriptor == null)
				return null;

			return actionDescriptor.ActionName;
		}
	}
}
