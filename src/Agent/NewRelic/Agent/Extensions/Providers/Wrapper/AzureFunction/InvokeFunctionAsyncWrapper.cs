// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AzureFunction
{
    public class InvokeFunctionAsyncWrapper : IWrapper
    {
        public bool IsTransactionRequired => false;

        private static bool _coldStart = true;
        private FunctionDetails _functionDetails;
        private static bool IsColdStart => _coldStart && !(_coldStart = false);

        private const string WrapperName = "AzureFunctionInvokeAsyncWrapper";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            Debugger.Break();

            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent,
            ITransaction transaction)
        {

            dynamic functionContext = instrumentedMethodCall.MethodCall.MethodArguments[0];

            if (functionContext == null)
            {
                return Delegates.NoOp;
            }

            _functionDetails = new FunctionDetails(functionContext);

            transaction = agent.CreateTransaction(
                isWeb: _functionDetails.Trigger == "http",
                category: "AzureFunction",
                transactionDisplayName: _functionDetails.FunctionName,
                doNotTrackAsUnitOfWork: true);

            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
                transaction.DetachFromPrimary(); //Remove from thread-local type storage
            }

            // TODO
            //if (IsColdStart) // only report this attribute if it's a cold start

            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, _functionDetails.FunctionName);

            return Delegates.GetAsyncDelegateFor<Task>(
                agent,
                segment,
                false,
                InvokeFunctionAsyncResponse,
                TaskContinuationOptions.ExecuteSynchronously);

            void InvokeFunctionAsyncResponse(Task responseTask)
            {
                if (responseTask.IsFaulted)
                {
                    //HandleError(segment, functionContext, responseTask, agent);
                    segment.End();
                    return;
                }

                segment.End();

                // do more stuff here

            }
        }

        private class FunctionDetails
        {
            public FunctionDetails(dynamic functionContext)
            {
                FunctionName = functionContext.FunctionDefinition.Name;
                InvocationId = functionContext.InvocationId;

                string type = functionContext.FunctionDefinition.InputBindings["req"].Type;
                Trigger = type.ResolveTriggerType();
            }

            public string FunctionName { get; private set; }

            public string Trigger { get; private set; }
            public string InvocationId { get; private set; }

        }

    }


    public static class TriggerTypeExtensions
    {
        public static string ResolveTriggerType(this string trigger)
        {
            switch (trigger)
            {
                case "httpTrigger":
                    return "http";
                default:
                    return trigger;
            }

        }
    }
}
