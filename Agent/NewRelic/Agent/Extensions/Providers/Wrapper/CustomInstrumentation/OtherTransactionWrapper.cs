using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.CustomInstrumentation
{
	public class OtherTransactionWrapper : IWrapper
	{
		[NotNull]
		private static readonly string[] PossibleWrapperNames = {
			"NewRelic.Agent.Core.Tracer.Factories.BackgroundThreadTracerFactory",
			"NewRelic.Providers.Wrapper.CustomInstrumentation.OtherTransactionWrapper",
			"NewRelic.Providers.Wrapper.CustomInstrumentationAsync.OtherTransactionWrapperAsync"
		};

		public bool IsTransactionRequired => false;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
		{
			var canWrap = !instrumentedMethodInfo.IsAsync
				&& PossibleWrapperNames.Contains(instrumentedMethodInfo.RequestedWrapperName);

			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			var typeName = instrumentedMethodCall.MethodCall.Method.Type;
			var methodName = instrumentedMethodCall.MethodCall.Method.MethodName;

			var name = $"{typeName}/{methodName}";
			
			transactionWrapperApi = instrumentedMethodCall.StartWebTransaction ?
				agentWrapperApi.CreateWebTransaction(WebTransactionType.Custom, name, false) :
				agentWrapperApi.CreateOtherTransaction("Custom", name, mustBeRootTransaction: false);

			var segment = !string.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName)
				? transactionWrapperApi.StartCustomSegment(instrumentedMethodCall.MethodCall, instrumentedMethodCall.RequestedMetricName)
				: transactionWrapperApi.StartTransactionSegment(instrumentedMethodCall.MethodCall, name);

			var hasMetricName = !string.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName);
			if (hasMetricName)
			{
				var priority = instrumentedMethodCall.RequestedTransactionNamePriority ?? TransactionNamePriority.Uri;
				transactionWrapperApi.SetCustomTransactionName(instrumentedMethodCall.RequestedMetricName, priority);
			}

			return Delegates.GetDelegateFor(
				onFailure: transactionWrapperApi.NoticeError,
				onComplete: () =>
				{
					segment.End();
					transactionWrapperApi.End();
				});
		}
	}
}
