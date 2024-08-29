// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
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
        private const string WrapperName = "AzureFunctionInvokeAsyncWrapper";

        private static bool _coldStart = true;
        private static bool IsColdStart => _coldStart && !(_coldStart = false);

        public bool IsTransactionRequired => false;

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

            var functionDetails = new FunctionDetails(functionContext, agent);
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
        private static Func<object, object> _functionDefinitionGetter;
        private static Func<object, object> _parametersGetter;
        private static Func<object, IReadOnlyDictionary<string, object>> _propertiesGetter;

        public FunctionDetails(dynamic functionContext, IAgent agent)
        {
            FunctionName = functionContext.FunctionDefinition.Name;
            InvocationId = functionContext.InvocationId;

            // cache the trigger by function name
            if (!_functionTriggerCache.TryGetValue(FunctionName, out string trigger))
            {
                // TODO: Needs null checks, optimization and possible caching of property accessors
                // functionContext.FunctionDefinition.Parameters is an ImmutableArray<FunctionParameter>
                var funcAsObj = (object)functionContext;
                _functionDefinitionGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(funcAsObj.GetType(), "FunctionDefinition");
                var functionDefinition = _functionDefinitionGetter(funcAsObj);

                _parametersGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(functionDefinition.GetType(), "Parameters");
                var parameters = _parametersGetter(functionDefinition) as IEnumerable;

                var foundTrigger = false;
                foreach (var parameter in parameters)
                {
                    // Properties is an IReadOnlyDictionary<string, object>
                    _propertiesGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<IReadOnlyDictionary<string, object>>(parameter.GetType(), "Properties");
                    var properties = _propertiesGetter(parameter);

                    //foreach (var pair in properties)
                    //{
                    //    agent.Logger.Info($"{pair.Key}:{pair.Value.GetType().Name}");
                    //}

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
                    {
                        break;
                    }
                }

                // shouldn't happen, as all functions are required to have a trigger
                if (!foundTrigger)
                {
                    agent.Logger.Debug($"Function {FunctionName} does not have a trigger, defaulting to 'other'");
                    Trigger = "other";
                }

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
