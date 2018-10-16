using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.CustomInstrumentationAsync
{
	public class OtherTransactionWrapperAsync : IWrapper
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
			var canWrap = instrumentedMethodInfo.IsAsync
				&& PossibleWrapperNames.Contains(instrumentedMethodInfo.RequestedWrapperName);

			if (!canWrap)
			{
				return new CanWrapResponse(false);
			}

			return TaskFriendlySyncContextValidator.CanWrapAsyncMethod("custom", "custom", instrumentedMethodInfo.Method.MethodName);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			var typeName = instrumentedMethodCall.MethodCall.Method.Type.FullName ?? "<unknown>";
			var methodName = instrumentedMethodCall.MethodCall.Method.MethodName;
			
			transactionWrapperApi = instrumentedMethodCall.StartWebTransaction ?
				agentWrapperApi.CreateWebTransaction(WebTransactionType.Custom, "Custom", false) :
				agentWrapperApi.CreateOtherTransaction("Custom", $"{typeName}/{methodName}", false);
			
			if (instrumentedMethodCall.IsAsync)
			{
				transactionWrapperApi.AttachToAsync();
			}

			var segment = !string.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName)
				? transactionWrapperApi.StartCustomSegment(instrumentedMethodCall.MethodCall, instrumentedMethodCall.RequestedMetricName)
				: transactionWrapperApi.StartMethodSegment(instrumentedMethodCall.MethodCall, typeName, methodName);
			
			var hasMetricName = !string.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName);
			if (hasMetricName)
			{
				var priority = instrumentedMethodCall.RequestedTransactionNamePriority ?? TransactionNamePriority.Uri;
				transactionWrapperApi.SetCustomTransactionName(instrumentedMethodCall.RequestedMetricName, priority);
			}

			return Delegates.GetDelegateFor<Task>(
				onFailure: ex =>
				{
					if (ex != null)
					{ 
						transactionWrapperApi.NoticeError(ex);
					}

					segment.End();
					transactionWrapperApi.End();
				},
				onSuccess: task =>
				{
					transactionWrapperApi.Detach();

					segment.RemoveSegmentFromCallStack();
					
					if (task == null)
						return;

					var context = SynchronizationContext.Current;
					if (context != null)
					{
						task.ContinueWith(responseTask => agentWrapperApi.HandleExceptions(() =>
						{
							if (responseTask != null && responseTask.IsFaulted && responseTask.Exception != null)
							{
								transactionWrapperApi.NoticeError(responseTask.Exception);
							}

							segment.End();
							transactionWrapperApi.End();
						}), TaskScheduler.FromCurrentSynchronizationContext());
					}
					else
					{
						task.ContinueWith(responseTask => agentWrapperApi.HandleExceptions(() =>
						{
							if (responseTask != null && responseTask.IsFaulted && responseTask.Exception != null)
							{ 
								transactionWrapperApi.NoticeError(responseTask.Exception);
							}

							segment.End();
							transactionWrapperApi.End();
						}), TaskContinuationOptions.ExecuteSynchronously);
					}
				});
		}
	}
}
