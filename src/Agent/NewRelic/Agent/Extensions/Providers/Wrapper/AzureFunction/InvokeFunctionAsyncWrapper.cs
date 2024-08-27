// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Collections.Concurrent;
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
                agent.Logger.Debug($"{WrapperName}: FunctionContext is null, can't instrument this invocation.");
                return Delegates.NoOp;
            }

            var functionDetails = new FunctionDetails(functionContext);
            // TODO: add validation for FunctionDetails? 

            transaction = agent.CreateTransaction(
                isWeb: functionDetails.IsWebTrigger,
                category: "AzureFunction", // TODO: Is this correct?
                transactionDisplayName: functionDetails.FunctionName,
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

            transaction.AddFaasAttribute("cloud.resource_id", AzureFunctionHelper.GetResourceIdWithFunctionName(functionDetails.FunctionName));
            transaction.AddFaasAttribute("faas.name", functionDetails.FunctionName);
            transaction.AddFaasAttribute("faas.trigger", functionDetails.Trigger);
            transaction.AddFaasAttribute("faas.invocation_id", functionDetails.InvocationId);

            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, functionDetails.FunctionName);

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
                        // TODO: add error handling here
                        //HandleError(segment, functionContext, responseTask, agent);
                        return;
                    }

                    // TODO: Do we need any additional work here?
                }
                finally
                {
                    segment.End();
                    transaction.End();
                }
            }
        }
    }

    internal class FunctionDetails
    {
        private static ConcurrentDictionary<string, string> _functionTriggerCache = new();

        public FunctionDetails(dynamic functionContext)
        {
            FunctionName = functionContext.FunctionDefinition.Name;
            InvocationId = functionContext.InvocationId;

            // cache the trigger by function name
            if (!_functionTriggerCache.TryGetValue(FunctionName, out string trigger))
            {
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
                            var triggerTypeName = propVal.GetType().Name;
                            Trigger = triggerTypeName.ResolveTriggerType();
                            foundTrigger = true;
                            break;
                        }
                    }
                    if (foundTrigger)
                        break;
                }

                if (!foundTrigger) // shouldn't happen, as all functions are required to have a trigger
                    Trigger = "other";

                _functionTriggerCache[FunctionName] = Trigger;
            }
            else
            {
                Trigger = trigger;
            }
        }

        public string FunctionName { get; private set; }

        public string Trigger { get; private set; }
        public string InvocationId { get; private set; }
        public bool IsWebTrigger => Trigger == "http";
    }

}
