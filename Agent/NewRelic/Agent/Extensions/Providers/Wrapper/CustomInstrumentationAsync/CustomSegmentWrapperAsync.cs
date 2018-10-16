using System;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.CustomInstrumentationAsync
{
	public class CustomSegmentWrapperAsync : IWrapper
	{
		[NotNull]
		private static readonly String[] PossibleWrapperNames = {
			"NewRelic.Providers.Wrapper.CustomInstrumentationAsync.CustomSegmentWrapperAsync",
		};

		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
		{
			if (PossibleWrapperNames.Contains(instrumentedMethodInfo.RequestedWrapperName))
			{

				return TaskFriendlySyncContextValidator.CanWrapAsyncMethod("custom", "custom", instrumentedMethodInfo.Method.MethodName);
			}
			else
			{
				return new CanWrapResponse(false);
			}
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			if (instrumentedMethodCall.IsAsync)
			{
				transactionWrapperApi.AttachToAsync();
			}

			//TODO: Consider breaking this out into a separate shared method used by sync and async.
			// find the first string argument
			String segmentName = null;
			foreach (var argument in instrumentedMethodCall.MethodCall.MethodArguments)
			{
				segmentName = argument as String;
				if (segmentName != null)
					break;
			}

			if (segmentName == null)
			{
				transactionWrapperApi.NoticeError(new ArgumentException("The CustomSegmentWrapperAsync can only be applied to a method with a String parameter."));
				return Delegates.NoOp;
			}

			var segment = transactionWrapperApi.StartCustomSegment(instrumentedMethodCall.MethodCall, segmentName);

			return Delegates.GetAsyncDelegateFor(agentWrapperApi, segment);
		}
	}
}
