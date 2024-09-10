// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.AzureFunction
{
    public class InvokeFunctionAsyncWrapper : IWrapper
    {
        private static bool _loggedDisabledMessage;
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
            if (!agent.Configuration.AzureFunctionModeEnabled) // bail early if azure function mode isn't enabled
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
                agent.Logger.Debug($"{WrapperName}: FunctionContext is null, can't instrument this invocation.");
                throw new ArgumentNullException("functionContext");
            }

            var functionDetails = new FunctionDetails(functionContext, agent);
            if (!functionDetails.IsValid())
            {
                agent.Logger.Debug($"{WrapperName}: FunctionDetails are invalid, can't instrument this invocation.");
                throw new Exception("FunctionDetails are missing some require information.");
            }

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
                transaction.AddFaasAttribute("faas.coldStart", "true");
            }

            transaction.AddFaasAttribute("cloud.resource_id", agent.Configuration.AzureFunctionResourceIdWithFunctionName(functionDetails.FunctionName));
            transaction.AddFaasAttribute("faas.name", functionDetails.FunctionName);
            transaction.AddFaasAttribute("faas.trigger", functionDetails.Trigger);
            transaction.AddFaasAttribute("faas.invocation_id", functionDetails.InvocationId);

            if (functionDetails.IsWebTrigger && !string.IsNullOrEmpty(functionDetails.RequestMethod))
            {
                transaction.SetRequestMethod(functionDetails.RequestMethod);
                transaction.SetUri(functionDetails.RequestPath);
            }

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

                    if (functionDetails.IsWebTrigger)
                    {
                        // GetInvocationResult is a static extension method
                        // there are multiple GetInvocationResult methods in this type; we want the one without any generic parameters
                        Type type = functionContext.GetType().Assembly.GetType("Microsoft.Azure.Functions.Worker.FunctionContextBindingFeatureExtensions");
                        var getInvocationResultMethod = type.GetMethods().Single(m => m.Name == "GetInvocationResult" && !m.ContainsGenericParameters);

                        dynamic invocationResult = getInvocationResultMethod.Invoke(null, new[] { functionContext });
                        var result = invocationResult?.Value;

                        // the result always seems to be of this type regardless of whether the app
                        // uses the Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore package or not
                        var resultTypeName = result?.GetType().Name;
                        if (resultTypeName == "GrpcHttpResponseData")
                        {
                            transaction.SetHttpResponseStatusCode((int)result.StatusCode);
                        }
                        else
                        {
                            agent.Logger.Debug($"Unexpected Azure Function invocationResult.Value type '{resultTypeName ?? "(null)"}' - unable to set http response status code.");
                        }
                    }

                }
                catch (Exception ex)
                {
                    agent.Logger.Error(ex, "Error processing Azure Function response.");
                    throw;
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
        private static MethodInfo _bindFunctionInputAsync;
        private static MethodInfo _genericFunctionInputBindingFeatureGetter;
        private static bool? _hasAspNetCoreExtensionsReference;

        private static readonly ConcurrentDictionary<string, string> _functionTriggerCache = new();
        private static Func<object, object> _functionDefinitionGetter;
        private static Func<object, object> _parametersGetter;
        private static Func<object, IReadOnlyDictionary<string, object>> _propertiesGetter;

        public FunctionDetails(dynamic functionContext, IAgent agent)
        {
            try
            {
                FunctionName = functionContext.FunctionDefinition.Name;
                InvocationId = functionContext.InvocationId;

                // cache the trigger by function name
                if (!_functionTriggerCache.TryGetValue(FunctionName, out string trigger))
                {
                    // functionContext.FunctionDefinition.Parameters is an ImmutableArray<FunctionParameter>
                    var funcAsObj = (object)functionContext;
                    _functionDefinitionGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(funcAsObj.GetType(), "FunctionDefinition");
                    var functionDefinition = _functionDefinitionGetter(funcAsObj);

                    _parametersGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(functionDefinition.GetType(), "Parameters");
                    var parameters = _parametersGetter(functionDefinition) as IEnumerable;

                    // Trigger is normally the first parameter, but we'll check all parameters to be sure.
                    var foundTrigger = false;
                    foreach (var parameter in parameters)
                    {
                        // Properties is an IReadOnlyDictionary<string, object>
                        _propertiesGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<IReadOnlyDictionary<string, object>>(parameter.GetType(), "Properties");
                        var properties = _propertiesGetter(parameter);
                        if (properties == null || properties.Count == 0)
                        {
                            continue;
                        }

                        if (!properties.TryGetValue("bindingAttribute", out var triggerAttribute))
                        {
                            foreach (var propVal in properties.Values)
                            {
                                if (propVal.GetType().Name.Contains("Trigger"))
                                {
                                    triggerAttribute = propVal;
                                    break;
                                }
                            }

                            if (triggerAttribute == null)
                            {
                                continue;
                            }
                        }

                        var triggerTypeName = triggerAttribute.GetType().Name;
                        Trigger = triggerTypeName.ResolveTriggerType();
                        foundTrigger = true;
                        break;
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

                if (IsWebTrigger)
                {
                    ParseRequestParameters(agent, functionContext);
                }
            }
            catch (Exception ex)
            {
                agent.Logger.Error(ex, "Error getting Azure Function details.");
                throw;
            }
        }

        private void ParseRequestParameters(IAgent agent, dynamic functionContext)
        {
            if (!_hasAspNetCoreExtensionsReference.HasValue)
            {
                // see if the Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore assembly is in the list of loaded assemblies
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                var assembly = loadedAssemblies.FirstOrDefault(a => a.GetName().Name == "Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore");
                _hasAspNetCoreExtensionsReference = assembly != null;

                if (_hasAspNetCoreExtensionsReference.Value)
                    agent.Logger.Debug("Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore assembly is loaded; not parsing request parameters in InvokeFunctionAsyncWrapper.");
            }

            // don't parse request parameters here if the Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore assembly is loaded
            // parsing occurs over in FunctionsHttpProxyingMiddlewareWrapper in that case
            if (_hasAspNetCoreExtensionsReference.Value)
            {
                return;
            }

            object features = functionContext.Features;

            if (_genericFunctionInputBindingFeatureGetter == null) // cache the methodinfo lookups for performance
            {
                var get = features.GetType().GetMethod("Get");
                if (get != null)
                {
                    _genericFunctionInputBindingFeatureGetter = get.MakeGenericMethod(features.GetType().Assembly.GetType("Microsoft.Azure.Functions.Worker.Context.Features.IFunctionInputBindingFeature"));
                }
                else
                {
                    agent.Logger.Debug("Unable to find FunctionContext.Features.Get method; unable to parse request parameters.");
                    return;
                }

                var bindFunctionInputType = features.GetType().Assembly.GetType("Microsoft.Azure.Functions.Worker.Context.Features.IFunctionInputBindingFeature");
                if (bindFunctionInputType == null)
                {
                    agent.Logger.Debug("Unable to find IFunctionInputBindingFeature type; unable to parse request parameters.");
                    return;
                }
                _bindFunctionInputAsync = bindFunctionInputType.GetMethod("BindFunctionInputAsync");
                if (_bindFunctionInputAsync == null)
                {
                    agent.Logger.Debug("Unable to find BindFunctionInputAsync method; unable to parse request parameters.");
                    return;
                }
            }

            if (_genericFunctionInputBindingFeatureGetter != null)
            {
                // Get the input binding feature and bind the input from the function context
                var inputBindingFeature = _genericFunctionInputBindingFeatureGetter.Invoke(features, new object[] { });
                dynamic valueTask = _bindFunctionInputAsync.Invoke(inputBindingFeature, new object[] { functionContext });
                valueTask.AsTask().Wait();
                var inputArguments = valueTask.Result.Values;
                var reqData = inputArguments[0];

                if (reqData != null && reqData.GetType().Name == "GrpcHttpRequestData" && !string.IsNullOrEmpty(reqData.Method))
                {
                    RequestMethod = reqData.Method;
                    Uri uri = reqData.Url;
                    RequestPath = uri.GetComponents(UriComponents.Path, UriFormat.Unescaped);
                }
            }
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(FunctionName) && !string.IsNullOrEmpty(Trigger) && !string.IsNullOrEmpty(InvocationId);
        }

        public string FunctionName { get; }

        public string Trigger { get; }
        public string InvocationId { get; }
        public bool IsWebTrigger => Trigger == "http";
        public string RequestMethod { get; private set; }
        public string RequestPath { get; private set; }
    }
}
