/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Threading;
using System.Threading.Tasks;
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

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Custom, $"{TypeName}/{MethodName}");
            var segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, TypeName, MethodName);

            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            return Delegates.GetDelegateFor<Task>(
                onFailure: ex =>
                {
                    if (ex != null)
                    {
                        transaction.NoticeError(ex);
                    }

                    segment.End();
                    transaction.End();
                },
                onSuccess: task =>
                {
                    transaction.Detach();

                    segment.RemoveSegmentFromCallStack();

                    if (task == null)
                    {
                        return;
                    }

                    Action<Task> taskCompletionHandler = (responseTask) => agentWrapperApi.HandleExceptions(() =>
                    {
                        if (responseTask.IsFaulted && (responseTask.Exception != null))
                        {
                            transaction.NoticeError(responseTask.Exception);
                        }

                        segment.End();
                        transaction.End();
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
