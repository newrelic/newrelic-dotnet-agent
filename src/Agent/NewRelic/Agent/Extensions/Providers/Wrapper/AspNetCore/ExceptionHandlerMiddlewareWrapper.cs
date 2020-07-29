/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AspNetCore
{
    public class ExceptionHandlerMiddlewareWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse("NewRelic.Providers.Wrapper.AspNetCore.ExceptionHandlerMiddlewareWrapper".Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            //Do nothing at the begining of the instrumented method.

            return Delegates.GetDelegateFor<Task>(onSuccess: HandleSuccess);

            void HandleSuccess(Task task)
            {
                task.ContinueWith(OnTaskCompletion);
            }

            void OnTaskCompletion(Task completedTask)
            {
                try
                {
                    var context = (HttpContext)instrumentedMethodCall.MethodCall.MethodArguments[0];

                    var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();

                    if (exceptionHandlerFeature != null)
                    {
                        transaction.NoticeError(exceptionHandlerFeature.Error);
                    }
                }
                catch (Exception ex)
                {
                    agentWrapperApi.SafeHandleException(ex);
                }
            }
        }
    }
}
