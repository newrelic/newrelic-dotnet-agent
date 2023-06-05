// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper
{
    public class OtherTransactionWrapper : IWrapper
    {
        private const string ForceNewTransactionOnAsyncWrapperName = "AsyncForceNewTransactionWrapper";

        private static readonly string[] PossibleWrapperNames =
        {
            "NewRelic.Agent.Core.Tracer.Factories.BackgroundThreadTracerFactory",
            "NewRelic.Providers.Wrapper.CustomInstrumentation.OtherTransactionWrapper",
            "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.OtherTransactionWrapperAsync",
            "OtherTransactionWrapper",
            ForceNewTransactionOnAsyncWrapperName
        };

        public bool IsTransactionRequired => false;

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {

            var currentTransaction = agent.CurrentTransaction;
            var transactionAlreadyExists = currentTransaction.IsValid;

            var typeName = instrumentedMethodCall.MethodCall.Method.Type.FullName ?? "<unknown>";
            var methodName = instrumentedMethodCall.MethodCall.Method.MethodName;

            var name = $"{typeName}/{methodName}";

            var trackWorkAsNewTransaction = false;

            //If the instrumentation indicates a desire to track this work as a separate transaction, check if this is possible
            if (agent.Configuration.ForceNewTransactionOnNewThread ||
                instrumentedMethodCall.InstrumentedMethodInfo.RequestedWrapperName == ForceNewTransactionOnAsyncWrapperName)
            {
                if (transactionAlreadyExists)
                {
                    trackWorkAsNewTransaction = agent.TryTrackAsyncWorkOnNewTransaction();

                    if (!trackWorkAsNewTransaction)
                    {
                        agent.Logger.Log(Extensions.Logging.Level.Debug, $"Ignoring request to track {name} as a separate transaction.  Only asynchronous work spawned on a new thread (e.g. Task.Run, TaskFactory.StartNew, or new Thread()) is supported at this time.");
                    }
                    else
                    {
                        agent.Logger.Log(Extensions.Logging.Level.Finest, $"Tracking call to method {name} under a separate transaction.");
                    }
                }
            }

            transaction = agent.CreateTransaction(instrumentedMethodCall.StartWebTransaction, "Custom", name, false);

            var newTransactionCreatedByWrapper = transaction.IsValid && (!transactionAlreadyExists || trackWorkAsNewTransaction);

            if (instrumentedMethodCall.IsAsync)
            {
                agent.CurrentTransaction.AttachToAsync();
            }

            var segment = !string.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName)
                ? transaction.StartCustomSegment(instrumentedMethodCall.MethodCall, instrumentedMethodCall.RequestedMetricName)
                : transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, typeName, methodName);

            var hasMetricName = !string.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName);
            if (hasMetricName)
            {
                var priority = instrumentedMethodCall.RequestedTransactionNamePriority ?? TransactionNamePriority.Uri;
                transaction.SetCustomTransactionName(instrumentedMethodCall.RequestedMetricName, priority);
            }

            return instrumentedMethodCall.IsAsync
                ? Delegates.GetDelegateFor<Task>(onFailure: onFailureAsync, onSuccess: onSuccessAsync)
                : Delegates.GetDelegateFor(onFailure: transaction.NoticeError, onComplete: onCompleteSync);


            void onCompleteSync()
            {
                segment.End();
                transaction.End(captureResponseTime: !transactionAlreadyExists);
            }

            void onFailureAsync(Exception ex)
            {
                if (ex != null)
                {
                    transaction.NoticeError(ex);
                }

                segment.End();
                transaction.End(captureResponseTime: !transactionAlreadyExists);
            }

            void onSuccessAsync(Task task)
            {
                if (newTransactionCreatedByWrapper)
                {
                    transaction.Detach();       //Detaches from both primary and async contexts.
                }

                segment.RemoveSegmentFromCallStack();

                // If the task is null, it means that the return type of the method is not of type Task.
                // Because we cannot add a continuation for segment timing, we cannot support these type of methods
                if (task == null)
                {
                    agent.Logger.Log(Extensions.Logging.Level.Debug, $"Warning, method {name} is an async method, but does not have a return type of Task.  This may prevent downstream instrumentation from being captured correctly.  Consider revising the method to have a return-type of Task.");

                    // Since we cannot add a continuation, we have no other choice than to end the segment and transaction here.
                    // This means we truncate the segment and potentially end the transaction prematurely preventing downstream instrumentation from being invoked.
                    segment.End();

                    // Also if this is a new transaction, we close it out as well.
                    if (newTransactionCreatedByWrapper)
                    {
                        transaction.End(captureResponseTime: !transactionAlreadyExists);
                    }

                    return;
                }

                var context = SynchronizationContext.Current;
                if (context != null)
                {
                    task.ContinueWith(responseTask => agent.HandleExceptions(() =>
                    {
                        if (responseTask != null && responseTask.IsFaulted && responseTask.Exception != null)
                        {
                            transaction.NoticeError(responseTask.Exception);
                        }

                        segment.End();
                        transaction.End(captureResponseTime: !transactionAlreadyExists);
                    }), TaskScheduler.FromCurrentSynchronizationContext()).GetAwaiter().GetResult();
                }
                else
                {
                    task.ContinueWith(responseTask => agent.HandleExceptions(() =>
                    {
                        if (responseTask != null && responseTask.IsFaulted && responseTask.Exception != null)
                        {
                            transaction.NoticeError(responseTask.Exception);
                        }

                        segment.End();
                        transaction.End(captureResponseTime: !transactionAlreadyExists);
                    }), TaskContinuationOptions.ExecuteSynchronously).GetAwaiter().GetResult();
                }
            }
        }

        public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            return new CanWrapResponse(PossibleWrapperNames.Contains(instrumentedMethodInfo.RequestedWrapperName, StringComparer.OrdinalIgnoreCase));
        }
    }
}
