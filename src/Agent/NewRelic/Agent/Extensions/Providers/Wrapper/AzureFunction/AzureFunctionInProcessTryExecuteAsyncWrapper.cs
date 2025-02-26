// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.AzureFunction;

public class AzureFunctionInProcessTryExecuteAsyncWrapper : IWrapper
{
    private static ConcurrentDictionary<string, InProcessFunctionDetails> _functionDetailsCache = new();
    private static Func<object, string> _fullNameGetter;
    private static Func<object, object> _functionDescriptorGetter;
    private static Func<object, Guid> _idGetter;

    private static bool _loggedDisabledMessage;

    private static bool _coldStart = true;
    private static bool IsColdStart => _coldStart && !(_coldStart = false);


    public bool IsTransactionRequired => false;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        return new CanWrapResponse(nameof(AzureFunctionInProcessTryExecuteAsyncWrapper).Equals(instrumentedMethodInfo.RequestedWrapperName));
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        if (!agent.Configuration.AzureFunctionModeEnabled) // bail early if azure function mode isn't enabled
        {
            if (!_loggedDisabledMessage)
            {
                agent.Logger.Info("Azure Function mode is not enabled; Azure Functions will not be instrumented.");
                _loggedDisabledMessage = true;
            }

            return Delegates.NoOp;
        }

        object functionInstance = instrumentedMethodCall.MethodCall.MethodArguments[0];

        _functionDescriptorGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(functionInstance.GetType(), "FunctionDescriptor");
        var functionDescriptor = _functionDescriptorGetter(functionInstance);

        _fullNameGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(functionDescriptor.GetType(), "FullName");
        string functionClassAndMethodName = _fullNameGetter(functionDescriptor);

        // cache the function details by function name so we only have to reflect on the function once
        var inProcessFunctionDetails = _functionDetailsCache.GetOrAdd(functionClassAndMethodName, _ => GetInProcessFunctionDetails(functionClassAndMethodName));

        _idGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<Guid>(functionInstance.GetType(), "Id");
        string invocationId = _idGetter(functionInstance).ToString();

        agent.Logger.Debug($"Instrumenting in-process Azure Function: {inProcessFunctionDetails.FunctionName} / invocation ID {invocationId} / Trigger {inProcessFunctionDetails.TriggerType}.");

        transaction = agent.CreateTransaction(
            isWeb: inProcessFunctionDetails.IsWebTrigger,
            category: "AzureFunction",
            transactionDisplayName: inProcessFunctionDetails.FunctionName,
            doNotTrackAsUnitOfWork: true);

        if (instrumentedMethodCall.IsAsync)
        {
            transaction.AttachToAsync();
            transaction.DetachFromPrimary(); //Remove from thread-local type storage
        }

        if (IsColdStart) // only report this attribute if it's a cold start
        {
            transaction.AddFaasAttribute("faas.coldStart", true);
        }

        transaction.AddFaasAttribute("cloud.resource_id", agent.Configuration.AzureFunctionResourceIdWithFunctionName(inProcessFunctionDetails.FunctionName));
        transaction.AddFaasAttribute("faas.name", $"{agent.Configuration.AzureFunctionAppName}/{inProcessFunctionDetails.FunctionName}");
        transaction.AddFaasAttribute("faas.trigger", inProcessFunctionDetails.TriggerType);
        transaction.AddFaasAttribute("faas.invocation_id", invocationId);

        var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, "Azure In-Proc Pipeline");

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
                    transaction.NoticeError(responseTask.Exception);
                }
            }
            finally
            {
                segment.End();
                transaction.End();
            }
        }
    }

    private InProcessFunctionDetails GetInProcessFunctionDetails(string functionClassAndMethodName)
    {
        string functionClassName = functionClassAndMethodName.Substring(0, functionClassAndMethodName.LastIndexOf('.'));
        string functionMethodName = functionClassAndMethodName.Substring(functionClassAndMethodName.LastIndexOf('.') + 1);

        // get the type for functionClassName from any loaded assembly, since it's not in the current assembly
        // TODO: is there a better way to do this?
        Type functionClassType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.FullName == functionClassName);

        MethodInfo functionMethod = functionClassType?.GetMethod(functionMethodName);
        var functionNameAttribute = functionMethod?.GetCustomAttributes().FirstOrDefault(a => a.GetType().Name == "FunctionNameAttribute");
        string functionName = functionNameAttribute?.GetType().GetProperty("Name")?.GetValue(functionNameAttribute) as string;

        var triggerAttributeParameter = functionMethod?.GetParameters().FirstOrDefault(p => p.GetCustomAttributes().Any(a => a.GetType().Name.Contains("TriggerAttribute")));
        var triggerAttribute = triggerAttributeParameter?.GetCustomAttributes().FirstOrDefault();
        string triggerAttributeName = triggerAttribute?.GetType().Name;
        string triggerType = triggerAttributeName?.ResolveTriggerType();

        var inProcessFunctionDetails = new InProcessFunctionDetails
        {
            TriggerType = triggerType,
            FunctionName = functionName,
        };

        if (triggerType == "pubsub" && triggerAttributeName == "ServiceBusTriggerAttribute") // add service bus trigger details if it's a service bus trigger
        {
            dynamic serviceBusTriggerAttribute = triggerAttribute;
            inProcessFunctionDetails.ServiceBusTriggerDetails = new ServiceBusTriggerDetails
            {
                QueueName = serviceBusTriggerAttribute.QueueName,
                TopicName = serviceBusTriggerAttribute.TopicName,
                SubscriptionName = serviceBusTriggerAttribute.SubscriptionName,
                Connection = serviceBusTriggerAttribute.Connection
            };
        }

        return inProcessFunctionDetails;
    }
}
