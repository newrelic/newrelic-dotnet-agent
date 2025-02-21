// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.AzureFunction;

internal class IsolatedFunctionDetails
{
    private static MethodInfo _bindFunctionInputAsync;
    private static MethodInfo _genericFunctionInputBindingFeatureGetter;
    private static bool? _hasAspNetCoreExtensionsReference;

    private static readonly ConcurrentDictionary<string, string> _functionTriggerCache = new();
    private static Func<object, object> _functionDefinitionGetter;
    private static Func<object, object> _parametersGetter;
    private static Func<object, IReadOnlyDictionary<string, object>> _propertiesGetter;

    private const string AspNetCoreExtensionsAssemblyName = "Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore";
    private const string IFunctionInputBindingFeatureTypeName = "Microsoft.Azure.Functions.Worker.Context.Features.IFunctionInputBindingFeature";

    public IsolatedFunctionDetails(dynamic functionContext, IAgent agent)
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
                ParseHttpTriggerParameters(agent, functionContext);
            }
        }
        catch (Exception ex)
        {
            agent.Logger.Error(ex, "Error getting Azure Function details.");
            throw;
        }
    }

    private void ParseHttpTriggerParameters(IAgent agent, dynamic functionContext)
    {
        if (!_hasAspNetCoreExtensionsReference.HasValue)
        {
            // see if the Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore assembly is in the list of loaded assemblies
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assembly = loadedAssemblies.FirstOrDefault(a => a.GetName().Name == AspNetCoreExtensionsAssemblyName);

            _hasAspNetCoreExtensionsReference = assembly != null;

            if (_hasAspNetCoreExtensionsReference.Value)
                agent.Logger.Debug($"{AspNetCoreExtensionsAssemblyName} assembly is loaded; InvokeFunctionAsyncWrapper will defer HttpTrigger parameter parsing to FunctionsHttpProxyingMiddlewareWrapper.");
        }

        // don't parse request parameters here if the Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore assembly is loaded.
        // If it is loaded, parsing occurs over in FunctionsHttpProxyingMiddlewareWrapper
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
                _genericFunctionInputBindingFeatureGetter = get.MakeGenericMethod(features.GetType().Assembly.GetType(IFunctionInputBindingFeatureTypeName));
            }
            else
            {
                agent.Logger.Debug("Unable to find FunctionContext.Features.Get method; unable to parse request parameters.");
                return;
            }

            var bindFunctionInputType = features.GetType().Assembly.GetType(IFunctionInputBindingFeatureTypeName);
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
            var inputBindingFeature = _genericFunctionInputBindingFeatureGetter.Invoke(features, []);
            dynamic valueTask = _bindFunctionInputAsync.Invoke(inputBindingFeature, [functionContext]);

            valueTask.AsTask().Wait(); // BindFunctionInputAsync returns a ValueTask, so we need to convert it to a Task to wait on it

            object[] inputArguments = valueTask.Result.Values;

            if (inputArguments is { Length: > 0 })
            {
                var reqData = (dynamic)inputArguments[0];

                if (reqData != null && reqData.GetType().Name == "GrpcHttpRequestData" && !string.IsNullOrEmpty(reqData.Method))
                {
                    RequestMethod = reqData.Method;
                    Uri uri = reqData.Url;
                    RequestPath = $"/{uri.GetComponents(UriComponents.Path, UriFormat.Unescaped)}"; // has to start with a slash
                }
            }
        }

        if (functionContext?.BindingContext?.BindingData is IReadOnlyDictionary<string, object> bindingData && bindingData.ContainsKey("Headers"))
        {
            // The headers are stored as a JSON blob.
            var headersJson = bindingData["Headers"].ToString();
            Headers = DictionaryHelpers.FromJson(headersJson);
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
    public IReadOnlyDictionary<string, object> Headers { get; private set; }
    public bool? HasAspNetCoreExtensionReference => _hasAspNetCoreExtensionsReference;
}
