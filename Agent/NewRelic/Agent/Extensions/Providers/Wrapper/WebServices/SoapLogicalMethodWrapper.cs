using System;
using System.Web.Services.Protocols;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.WebServices
{
	public class SoapLogicalMethodWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(
				assemblyName: "System.Web.Services",
				typeName: "System.Web.Services.Protocols.LogicalMethodInfo",
				methodNames: new[] { "Invoke", "BeginInvoke" }
			);
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			var logicalMethodInfo = instrumentedMethodCall.MethodCall.InvocationTarget as LogicalMethodInfo;
			if (logicalMethodInfo == null)
				throw new NullReferenceException("LogicalMethodInfo was expected.");

			var declaringType = logicalMethodInfo.DeclaringType;
			var methodName = logicalMethodInfo.Name;

			var name = String.Format("{0}.{1}", declaringType.FullName, methodName);

			transactionWrapperApi.SetWebTransactionName(WebTransactionType.WebService, name, TransactionNamePriority.FrameworkLow);
			var segment = transactionWrapperApi.StartTransactionSegment(instrumentedMethodCall.MethodCall, name);

			return Delegates.GetDelegateFor(
				onFailure: OnFailure,
				onComplete: segment.End
				);

			void OnFailure(Exception ex)
			{
				if (ex is System.Reflection.TargetInvocationException && ex.InnerException != null)
				{
					ex = ex.InnerException;
				}

				if (ex != null)
				{
					transactionWrapperApi.NoticeError(ex);
				}
			}
		}
	}
}