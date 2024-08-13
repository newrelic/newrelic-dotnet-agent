// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.AzureFunction;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

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
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent,
            ITransaction transaction)
        {

            dynamic functionContext = instrumentedMethodCall.MethodCall.MethodArguments[0];

            if (functionContext == null)
            {
                // TODO: logging here?
                return Delegates.NoOp;
            }

            _functionDetails = new FunctionDetails(functionContext);
            // TODO: add validation for FunctionDetails? 

            transaction = agent.CreateTransaction(
                isWeb: _functionDetails.Trigger == "http",
                category: "AzureFunction", // TODO: Is this correct?
                transactionDisplayName: _functionDetails.FunctionName,
                doNotTrackAsUnitOfWork: true);

            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
                transaction.DetachFromPrimary(); //Remove from thread-local type storage
            }

            if (IsColdStart) // only report this attribute if it's a cold start
            {
                transaction.AddFaasAttribute("faas.coldStart", "true");
            }

            transaction.AddFaasAttribute("cloud.resource_id", AzureFunctionHelper.GetResourceIdWithFunctionName(_functionDetails.FunctionName));
            transaction.AddFaasAttribute("faas.name", _functionDetails.FunctionName);
            transaction.AddFaasAttribute("faas.trigger", _functionDetails.Trigger);
            transaction.AddFaasAttribute("faas.invocation_id", _functionDetails.InvocationId);

            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, _functionDetails.FunctionName);

            return Delegates.GetAsyncDelegateFor<Task>(
                agent,
                segment,
                false,
                InvokeFunctionAsyncResponse,
                TaskContinuationOptions.ExecuteSynchronously);

            void InvokeFunctionAsyncResponse(Task responseTask)
            {
                try
                {
                    if (responseTask.IsFaulted)
                    {
                        // TODO: add error handling here? 
                        //HandleError(segment, functionContext, responseTask, agent);
                        return;
                    }
                }
                finally
                {
                    segment.End();
                    transaction.End();
                }
            }
        }

        private class FunctionDetails
        {
            public FunctionDetails(dynamic functionContext)
            {
                FunctionName = functionContext.FunctionDefinition.Name;
                InvocationId = functionContext.InvocationId;

                // TODO: Needs null checks, optimization and possible caching of property accessors
                // functionContext.FunctionDefinition.Parameters is an ImmutableArray<FunctionParameter>
                var funcAsObj = (object)functionContext;
                var functionDefinitionGetter = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(funcAsObj.GetType(), "FunctionDefinition");
                var functionDefinition = functionDefinitionGetter(funcAsObj);
                var parametersGetter = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(functionDefinition.GetType(), "Parameters");
                var parameters = parametersGetter(functionDefinition) as IEnumerable;
                bool foundTrigger = false;
                foreach (var parameter in parameters)
                {
                    // Properties is an IReadOnlyDictionary<string, object>
                    var propertiesGetter = VisibilityBypasser.Instance.GeneratePropertyAccessor<IReadOnlyDictionary<string, object>>(parameter.GetType(), "Properties");
                    var properties = propertiesGetter(parameter);
                    foreach (var propVal in properties.Values)
                    {
                        if (propVal.GetType().Name.Contains("Trigger"))
                        {
                            var trigger = propVal.GetType().Name;
                            Trigger = trigger.ResolveTriggerType();
                            foundTrigger = true;
                            break;
                        }
                    }
                    if (foundTrigger)
                        break;
                }

                if (!foundTrigger) // shouldn't happen, as all functions are required to have a trigger
                    Trigger = "other";
            }

            public string FunctionName { get; private set; }

            public string Trigger { get; private set; }
            public string InvocationId { get; private set; }

        }

    }
}
