using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper
{
	public class DefaultWrapperAsync : IDefaultWrapper
	{
		[NotNull]
		private static readonly string[] PossibleWrapperNames = {
			"NewRelic.Agent.Core.Wrapper.DefaultWrapper",
			"NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync",

			// To support older custom instrumentation we need to also accept the old tracer factory name
			"NewRelic.Agent.Core.Tracer.Factories.DefaultTracerFactory"
		};

		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
		{
			var canWrap = instrumentedMethodInfo.IsAsync
				&& PossibleWrapperNames.Contains(instrumentedMethodInfo.RequestedWrapperName);

			if (!canWrap)
			{
				return new CanWrapResponse(false);
			}

			return TaskFriendlySyncContextValidator.CanWrapAsyncMethod("custom", "custom", instrumentedMethodInfo.Method.MethodName);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, [NotNull] IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			if (instrumentedMethodCall.IsAsync)
			{
				transaction.AttachToAsync();
			}

			var typeName = instrumentedMethodCall.MethodCall.Method.Type.FullName ?? "<unknown>";
			var methodName = instrumentedMethodCall.MethodCall.Method.MethodName;
			var segment = !string.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName)
				? transaction.StartCustomSegment(instrumentedMethodCall.MethodCall, instrumentedMethodCall.RequestedMetricName)
				: transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, typeName, methodName);

			//Only override transaction name if priority set since this is segment-level instrumentation
			if (!string.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName) && instrumentedMethodCall.RequestedTransactionNamePriority.HasValue)
			{
				transaction.SetCustomTransactionName(instrumentedMethodCall.RequestedMetricName, instrumentedMethodCall.RequestedTransactionNamePriority.Value);
			}

			return Delegates.GetAsyncDelegateFor(agentWrapperApi, segment);
		}
	}
}
