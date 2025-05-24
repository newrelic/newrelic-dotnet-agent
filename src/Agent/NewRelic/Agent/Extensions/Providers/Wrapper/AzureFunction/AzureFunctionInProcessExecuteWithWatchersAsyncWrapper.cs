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

public class AzureFunctionInProcessExecuteWithWatchersAsyncWrapper : IWrapper
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
        return new CanWrapResponse(nameof(AzureFunctionInProcessExecuteWithWatchersAsyncWrapper).Equals(instrumentedMethodInfo.RequestedWrapperName));
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

        agent.Logger.Finest("Instrumenting in-process Azure Function: {FunctionName} / invocation ID {invocationId} / Trigger {Trigger}.", inProcessFunctionDetails.FunctionName, invocationId, inProcessFunctionDetails.Trigger);

        agent.RecordSupportabilityMetric("Dotnet/AzureFunction/Worker/InProcess");
        agent.RecordSupportabilityMetric($"Dotnet/AzureFunction/Trigger/{inProcessFunctionDetails.TriggerTypeName ?? "unknown"}");

        transaction = agent.CreateTransaction(
            isWeb: inProcessFunctionDetails.IsWebTrigger,
            category: "AzureFunction",
            transactionDisplayName: inProcessFunctionDetails.FunctionName,
            doNotTrackAsUnitOfWork: false);

        if (instrumentedMethodCall.IsAsync)
        {
            transaction.AttachToAsync();
            transaction.DetachFromPrimary(); //Remove from thread-local type storage
        }

        if (inProcessFunctionDetails.IsWebTrigger)
        {
            transaction.SetWebTransactionName("AzureFunction", inProcessFunctionDetails.FunctionName, TransactionNamePriority.FrameworkHigh);
        }
        else
        {
            transaction.SetOtherTransactionName("AzureFunction", inProcessFunctionDetails.FunctionName, TransactionNamePriority.FrameworkHigh);
        }

        if (IsColdStart) // only report this attribute if it's a cold start
        {
            transaction.AddFaasAttribute("faas.coldStart", true);
        }

        transaction.AddFaasAttribute("cloud.resource_id", agent.Configuration.AzureFunctionResourceIdWithFunctionName(inProcessFunctionDetails.FunctionName));
        transaction.AddFaasAttribute("faas.name", $"{agent.Configuration.AzureFunctionAppName}/{inProcessFunctionDetails.FunctionName}");
        transaction.AddFaasAttribute("faas.trigger", inProcessFunctionDetails.Trigger);
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
            Trigger = triggerType,
            TriggerTypeName = triggerAttributeName?.Replace("TriggerAttribute", string.Empty),
            FunctionName = functionName,
        };

        return inProcessFunctionDetails;
    }
}
