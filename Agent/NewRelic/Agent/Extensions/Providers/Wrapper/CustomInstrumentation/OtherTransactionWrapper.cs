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

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			var typeName = instrumentedMethodCall.MethodCall.Method.Type;
			var methodName = instrumentedMethodCall.MethodCall.Method.MethodName;

			var name = $"{typeName}/{methodName}";
			
			transaction = instrumentedMethodCall.StartWebTransaction ?
				agentWrapperApi.CreateWebTransaction(WebTransactionType.Custom, name, false) :
				agentWrapperApi.CreateOtherTransaction("Custom", name, mustBeRootTransaction: false);

			var segment = !string.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName)
				? transaction.StartCustomSegment(instrumentedMethodCall.MethodCall, instrumentedMethodCall.RequestedMetricName)
				: transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, name);

			var hasMetricName = !string.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName);
			if (hasMetricName)
			{
				var priority = instrumentedMethodCall.RequestedTransactionNamePriority ?? 1;
				transaction.SetCustomTransactionName(instrumentedMethodCall.RequestedMetricName, priority);
			}

			return Delegates.GetDelegateFor(
				onFailure: transaction.NoticeError,
				onComplete: () =>
				{
					segment.End();
					transaction.End();
				});
		}
	}
}
