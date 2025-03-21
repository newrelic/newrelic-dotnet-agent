// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AzureFunction;

public class AzureFunctionIsolatedInvokeAsyncWrapper : IWrapper
{
    private static MethodInfo _getInvocationResultMethod;
    private static bool _loggedDisabledMessage;

    private static bool _coldStart = true;
    private static bool IsColdStart => _coldStart && !(_coldStart = false);

    public bool IsTransactionRequired => false;

    private const string FunctionContextBindingFeatureExtensionsTypeName = "Microsoft.Azure.Functions.Worker.FunctionContextBindingFeatureExtensions";

    public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
    {
        return new CanWrapResponse(nameof(AzureFunctionIsolatedInvokeAsyncWrapper).Equals(methodInfo.RequestedWrapperName));
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent,
        ITransaction transaction)
    {
        if (!(agent.Configuration.AzureFunctionModeDetected && agent.Configuration.AzureFunctionModeEnabled) ) // bail early if azure function mode isn't enabled
        {
            if (!_loggedDisabledMessage)
            {
                agent.Logger.Info("Azure Function mode is not enabled; Azure Functions will not be instrumented.");
                _loggedDisabledMessage = true;
            }

            return Delegates.NoOp;
        }

        dynamic functionContext = instrumentedMethodCall.MethodCall.MethodArguments[0];

        if (functionContext == null)
        {
            agent.Logger.Debug($"{nameof(AzureFunctionIsolatedInvokeAsyncWrapper)}: FunctionContext is null, can't instrument this invocation.");
            throw new ArgumentNullException("functionContext");
        }

        var functionDetails = new IsolatedFunctionDetails(functionContext, agent);
        if (!functionDetails.IsValid())
        {
            agent.Logger.Debug($"{nameof(AzureFunctionIsolatedInvokeAsyncWrapper)}: FunctionDetails are invalid, can't instrument this invocation.");
            throw new Exception("FunctionDetails are missing some require information.");
        }

        agent.RecordSupportabilityMetric("Dotnet/AzureFunction/Worker/Isolated");
        agent.RecordSupportabilityMetric($"Dotnet/AzureFunction/Trigger/{functionDetails.TriggerTypeName ?? "unknown"}");

        transaction = agent.CreateTransaction(
            isWeb: functionDetails.IsWebTrigger,
            category: "AzureFunction",
            transactionDisplayName: functionDetails.FunctionName,
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

        transaction.AddFaasAttribute("cloud.resource_id", agent.Configuration.AzureFunctionResourceIdWithFunctionName(functionDetails.FunctionName));
        transaction.AddFaasAttribute("faas.name", $"{agent.Configuration.AzureFunctionAppName}/{functionDetails.FunctionName}");
        transaction.AddFaasAttribute("faas.trigger", functionDetails.Trigger);
        transaction.AddFaasAttribute("faas.invocation_id", functionDetails.InvocationId);

        if (functionDetails.IsWebTrigger && !string.IsNullOrEmpty(functionDetails.RequestMethod))
        {
            transaction.SetRequestMethod(functionDetails.RequestMethod);
            transaction.SetUri(functionDetails.RequestPath);

            if (functionDetails.Headers?.Count != 0)
            {
                transaction.AcceptDistributedTraceHeaders(functionDetails.Headers, GetHeaderValue, TransportType.HTTP);
            }
        }
        // save for later
        //else if (functionDetails.TriggerTypeName == "ServiceBus")
        //{
        //    if (functionDetails.Headers?.Count != 0)
        //    {
        //        transaction.AcceptDistributedTraceHeaders(functionDetails.Headers, GetHeaderValue, TransportType.Queue);
        //    }
        //}

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
                    transaction.NoticeError(responseTask.Exception);
                    return;
                }

                if (_getInvocationResultMethod == null)
                {
                    // GetInvocationResult is a static extension method
                    // there are multiple GetInvocationResult methods in this type; we want the one without any generic parameters
                    Type type = functionContext.GetType().Assembly.GetType(FunctionContextBindingFeatureExtensionsTypeName);
                    _getInvocationResultMethod = type.GetMethods().Single(m => m.Name == "GetInvocationResult" && !m.ContainsGenericParameters);
                }
                dynamic invocationResult = _getInvocationResultMethod.Invoke(null, new[] { functionContext });
                var result = invocationResult?.Value;

                // only pull response status code here if it's a web trigger and the Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore assembly is not loaded.
                if (functionDetails.IsWebTrigger && functionDetails.HasAspNetCoreExtensionReference != null && !functionDetails.HasAspNetCoreExtensionReference.Value)
                {
                    if (result != null && result.StatusCode != null)
                    {
                        var statusCode = result.StatusCode;
                        transaction.SetHttpResponseStatusCode((int)statusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                agent.Logger.Warn(ex, "Error processing Azure Function response.");
                throw;
            }
            finally
            {
                segment.End();
                transaction.End();
            }
        }

        IEnumerable<string> GetHeaderValue(IReadOnlyDictionary<string, object> headers, string key)
        {
            if (!headers.ContainsKey(key))
                return [];

            return [headers[key].ToString()];
        }
    }
}
