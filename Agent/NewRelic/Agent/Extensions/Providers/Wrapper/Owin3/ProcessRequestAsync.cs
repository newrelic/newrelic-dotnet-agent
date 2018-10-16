using System;
using System.Threading.Tasks;
using System.Threading;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Owin3
{
	/// <summary>
	/// This instrumentation is used for OWIN 3
	/// </summary>
	public class ProcessRequestAsync : IWrapper
	{
		private const string AssemblyName = "Microsoft.Owin.Host.HttpListener";
		private const string TypeName = "Microsoft.Owin.Host.HttpListener.OwinHttpListener";
		private const string MethodName = "ProcessRequestAsync";

		public bool IsTransactionRequired => false;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
		{
			var method = instrumentedMethodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: AssemblyName, typeName: TypeName, methodName: MethodName);
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, [NotNull] IAgentWrapperApi agentWrapperApi, [CanBeNull] ITransactionWrapperApi transactionWrapperApi)
		{
			transactionWrapperApi = agentWrapperApi.CreateWebTransaction(WebTransactionType.Custom, $"{TypeName}/{MethodName}");
			var segment = transactionWrapperApi.StartMethodSegment(instrumentedMethodCall.MethodCall, TypeName, MethodName);

			if (instrumentedMethodCall.IsAsync)
			{
				transactionWrapperApi.AttachToAsync();
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
					{
						return;
					}

					Action<Task> taskCompletionHandler = (responseTask) => agentWrapperApi.HandleExceptions(() =>
					{
						if (responseTask.IsFaulted && (responseTask.Exception != null))
						{
							transactionWrapperApi.NoticeError(responseTask.Exception);
						}

						segment.End();
						transactionWrapperApi.End();
					});

					var context = SynchronizationContext.Current;
					if (context != null)
					{
						task.ContinueWith(taskCompletionHandler, TaskScheduler.FromCurrentSynchronizationContext());
					}
					else
					{
						task.ContinueWith(taskCompletionHandler, TaskContinuationOptions.ExecuteSynchronously);
					}
				});
		}
	}
}
