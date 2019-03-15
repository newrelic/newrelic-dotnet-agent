using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.CustomInstrumentation
{
	public class OtherTransactionWrapper : IWrapper
	{
		private const string ForceNewTransansactionOnAsyncWrapperName = "AsyncForceNewTransactionWrapper";

		private static readonly string[] PossibleWrapperNames = 
		{
			"NewRelic.Agent.Core.Tracer.Factories.BackgroundThreadTracerFactory",
			"NewRelic.Providers.Wrapper.CustomInstrumentation.OtherTransactionWrapper",
			"NewRelic.Providers.Wrapper.CustomInstrumentationAsync.OtherTransactionWrapperAsync",
			"OtherTransactionWrapper",
			ForceNewTransansactionOnAsyncWrapperName
		};

		public bool IsTransactionRequired => false;

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{

			var currentTransaction = agentWrapperApi.CurrentTransactionWrapperApi;
			var transactionAlreadyExists = currentTransaction.IsValid;

			var typeName = instrumentedMethodCall.MethodCall.Method.Type.FullName ?? "<unknown>";
			var methodName = instrumentedMethodCall.MethodCall.Method.MethodName;

			var name = $"{typeName}/{methodName}";

			var trackWorkAsNewTransaction = false;

			//If the instrumentation indicates a desire to track this work as a separate transaction, check if this is possible
			if (instrumentedMethodCall.InstrumentedMethodInfo.RequestedWrapperName == ForceNewTransansactionOnAsyncWrapperName)
			{
				if (!transactionAlreadyExists)
				{
					agentWrapperApi.Logger.Log(Agent.Extensions.Logging.Level.Debug, $"Ignoring request to track {name} as a separate transaction because this call is not already encapsulated within a transaction.");
				}
				else
				{
					trackWorkAsNewTransaction = agentWrapperApi.TryTrackAsyncWorkOnNewTransaction();

					if (!trackWorkAsNewTransaction)
					{
						agentWrapperApi.Logger.Log(Agent.Extensions.Logging.Level.Debug, $"Ignoring request to track {name} as a separate transaction.  Only asynchronous work spawned on a new thread (e.g. Task.Run, TaskFactory.StartNew, or new Thread()) is supported at this time.");
					}
					else
					{
						agentWrapperApi.Logger.Log(Agent.Extensions.Logging.Level.Finest, $"Tracking call to method {name} under a separate transaction.");
					}
				}
			}

			transactionWrapperApi = agentWrapperApi.CreateTransaction(instrumentedMethodCall.StartWebTransaction, "Custom", name, false);

			var newTransactionCreatedByWrapper = transactionWrapperApi.IsValid && (!transactionAlreadyExists || trackWorkAsNewTransaction);

			if (instrumentedMethodCall.IsAsync)
			{
				agentWrapperApi.CurrentTransactionWrapperApi.AttachToAsync();
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
			
			return instrumentedMethodCall.IsAsync
				? Delegates.GetDelegateFor<Task>(onFailure: onFailureAsync, onSuccess: onSuccessAsync)
				: Delegates.GetDelegateFor(onFailure: transactionWrapperApi.NoticeError, onComplete: onCompleteSync);


			void onCompleteSync()
			{
				segment.End();
				transactionWrapperApi.End();
			}

			void onFailureAsync(Exception ex)
			{
				if (ex != null)
				{
					transactionWrapperApi.NoticeError(ex);
				}

				segment.End();
				transactionWrapperApi.End();
			}

			void onSuccessAsync(Task task)
			{
				if (newTransactionCreatedByWrapper)
				{
					transactionWrapperApi.Detach();		//Detaches from both primary and async contexts.
				}

				segment.RemoveSegmentFromCallStack();

				// If the task is null, it means that the return type of the method is not of type Task.
				// Because we cannot add a continuation for segment timing, we cannot support these type of methods
				if (task == null)
				{
					agentWrapperApi.Logger.Log(Agent.Extensions.Logging.Level.Debug, $"Warning, method {name} is an async method, but does not have a return type of Task.  This may prevent downstream instrumentation from being captured correctly.  Consider revising the method to have a return-type of Task.");

					// Since we cannot add a continuation, we have no other choice than to end the segment and tranaction here.
					// This means we truncate the segment and potentially end the transaction prematurely preventing downstream instrumentation from being invoked.
					segment.End();

					// Also if this is a new transaction, we close it out as well.
					if (newTransactionCreatedByWrapper)
					{
						transactionWrapperApi.End();
					}

					return;
				}

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
			}
		}

		public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
		{
			return new CanWrapResponse(PossibleWrapperNames.Contains(instrumentedMethodInfo.RequestedWrapperName));
		}
	}
}
