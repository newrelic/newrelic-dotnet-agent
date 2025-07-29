// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Core.Tracer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Api;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Wrapper
{
    public interface IWrapperService
    {
        AfterWrappedMethodDelegate BeforeWrappedMethod(Type type, string methodName, string argumentSignature, object invocationTarget, object[] methodArguments, string tracerFactoryName, string metricName, uint tracerArguments, ulong functionId);
        void ClearCaches();
    }

    public class WrapperService : IWrapperService
    {
        private readonly int _maxConsecutiveFailures;
        private readonly IConfigurationService _configurationService;
        private readonly IAgent _agent;
        private readonly IWrapperMap _wrapperMap;
        private readonly IAgentHealthReporter _agentHealthReporter;
        private readonly IAgentTimerService _agentTimerService;

        private class InstrumentedMethodInfoWrapper
        {
            public readonly InstrumentedMethodInfo instrumentedMethodInfo;
            public readonly TrackedWrapper wrapper;

            public InstrumentedMethodInfoWrapper(InstrumentedMethodInfo instrumentedMethodInfo, TrackedWrapper wrapper)
            {
                this.wrapper = wrapper;
                this.instrumentedMethodInfo = instrumentedMethodInfo;
            }
        }

        private readonly ConcurrentDictionary<ulong, InstrumentedMethodInfoWrapper> _functionIdToWrapper;
        private readonly List<string> KnownCustomTracerNames = new List<string>
        {
            "NewRelic.Agent.Core.Tracer.Factories.BackgroundThreadTracerFactory",
            "NewRelic.Agent.Core.Tracer.Factories.IgnoreTransactionTracerFactory",
            "NewRelic.Providers.Wrapper.CustomInstrumentation.OtherTransactionWrapper",
            "AsyncForceNewTransactionWrapper",
            "OtherTransactionWrapper"
        };

        public WrapperService(IConfigurationService configurationService, IWrapperMap wrapperMap, IAgent agent, IAgentHealthReporter agentHealthReporter, IAgentTimerService agentTimerService)
        {
            _configurationService = configurationService;
            _maxConsecutiveFailures = configurationService.Configuration.WrapperExceptionLimit;
            _agent = agent;
            _wrapperMap = wrapperMap;
            _agentHealthReporter = agentHealthReporter;
            _agentTimerService = agentTimerService;
            _functionIdToWrapper = new ConcurrentDictionary<ulong, InstrumentedMethodInfoWrapper>();
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(Type type, string methodName, string argumentSignature,
            object invocationTarget, object[] methodArguments, string tracerFactoryName, string metricName,
            uint tracerArguments, ulong functionId)
        {
            InstrumentedMethodInfo instrumentedMethodInfo = default(InstrumentedMethodInfo);
            TrackedWrapper trackedWrapper;
            if (_functionIdToWrapper.TryGetValue(functionId, out InstrumentedMethodInfoWrapper methodAndWrapper))
            {
                instrumentedMethodInfo = methodAndWrapper.instrumentedMethodInfo;
                trackedWrapper = methodAndWrapper.wrapper;
            }
            else
            {
                bool isCustom = KnownCustomTracerNames.Contains(tracerFactoryName);
                var isAsync = TracerArgument.IsAsync(tracerArguments);

                if (TracerArgument.IsFlagSet(tracerArguments, TracerFlags.AttributeInstrumentation))
                {
                    tracerFactoryName = ResolveTracerFactoryNameForAttributeInstrumentation(tracerArguments, isAsync, tracerFactoryName);
                    isCustom = true;
                }

                var method = new Method(type, methodName, argumentSignature, functionId.GetHashCode());
                var transactionNamePriority = TracerArgument.GetTransactionNamingPriority(tracerArguments);
                instrumentedMethodInfo = new InstrumentedMethodInfo((long)functionId, method, tracerFactoryName, isAsync, metricName, transactionNamePriority, TracerArgument.IsFlagSet(tracerArguments, TracerFlags.WebTransaction));

                trackedWrapper = _wrapperMap.Get(instrumentedMethodInfo);

                if (trackedWrapper == null)
                {
                    Log.Warn("WrapperMap.Get unexpectedly returned null for {0}.{1}({2}) in assembly [{3}] (requested wrapper name was {4}).",
                        instrumentedMethodInfo.Method.Type.FullName,
                        instrumentedMethodInfo.Method.MethodName,
                        instrumentedMethodInfo.Method.ParameterTypeNames,
                        instrumentedMethodInfo.Method.Type.Assembly.FullName,
                        instrumentedMethodInfo.RequestedWrapperName);

                    return null;
                }

                if (isAsync)
                {
                    try
                    {
                        var returnType = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).ReturnType;
                        if ((returnType != typeof(Task)) &&
                            (!returnType.IsGenericType || (returnType.GetGenericTypeDefinition() != typeof(Task<>))))
                        {
                            Log.Warn("Instrumenting async methods that return a type other than Task or Task<> is not supported and may result in inconsistent data. '{0}' has a return type of '{1}'.", methodName, returnType?.Name);
                        }
                    }
                    catch
                    {
                        // Since this is just for logging purposes it doesn't matter if it fails
                    }
                }

                _functionIdToWrapper[functionId] = new InstrumentedMethodInfoWrapper(instrumentedMethodInfo, trackedWrapper);
                GenerateSupportabilityMetrics(instrumentedMethodInfo, isCustom);
            }

            var wrapper = trackedWrapper.Wrapper;

            var transaction = _agent.CurrentTransaction;

            if (Log.IsFinestEnabled)
            {
                transaction.LogFinest($"Attempting to execute {wrapper} found from InstrumentedMethodInfo: {instrumentedMethodInfo}");
            }

            if (transaction.IsFinished)
            {
                if (Log.IsFinestEnabled)
                {
                    if (wrapper.IsTransactionRequired)
                    {
                        transaction.LogFinest($"Transaction has already ended, skipping method {type.FullName}.{methodName}({argumentSignature}).");
                    }
                    else
                    {
                        transaction.LogFinest("Transaction has already ended, detaching from transaction storage context.");
                    }
                }

                transaction.Detach();
                transaction = _agent.CurrentTransaction;

                if (wrapper.IsTransactionRequired)
                {
                    return Delegates.NoOp;
                }
            }

            if (wrapper.IsTransactionRequired)
            {
                if (!transaction.IsValid)
                {
                    if (Log.IsFinestEnabled)
                    {
                        transaction.LogFinest($"No transaction, skipping method {type.FullName}.{methodName}({argumentSignature})");
                    }

                    return Delegates.NoOp;
                }

                if (transaction.CurrentSegment.IsLeaf)
                {
                    return Delegates.NoOp;
                }
            }

            var methodCall = new MethodCall(instrumentedMethodInfo.Method, invocationTarget, methodArguments, instrumentedMethodInfo.IsAsync);
            var instrumentedMethodCall = new InstrumentedMethodCall(methodCall, instrumentedMethodInfo);

            // if the wrapper throws an exception when executing the pre-method code, make sure the wrapper isn't called again in the future
            try
            {
                using (_agentTimerService.StartNew("BeforeWrappedMethod", type.FullName, methodName))
                {
                    var afterWrappedMethod = wrapper.BeforeWrappedMethod(instrumentedMethodCall, _agent, transaction);
                    return (result, exception) =>
                    {
                        using (_agentTimerService.StartNew("AfterWrappedMethod", type.FullName, methodName))
                        {
                            // if the wrapper throws an exception when executing the post-method code, make sure the wrapper isn't called again in the future
                            try
                            {
                                afterWrappedMethod(result, exception);
                                trackedWrapper.NoticeSuccess();
                            }
                            catch (Exception)
                            {
                                HandleBeforeWrappedMethodException(functionId, trackedWrapper, instrumentedMethodCall, instrumentedMethodInfo);
                                throw;
                            }
                        }
                    };
                }
            }
            catch
            {
                HandleBeforeWrappedMethodException(functionId, trackedWrapper, instrumentedMethodCall, instrumentedMethodInfo);
                throw;
            }
        }

        public void ClearCaches()
        {
            _functionIdToWrapper.Clear();
        }

        public static string ResolveTracerFactoryNameForAttributeInstrumentation(uint tracerArguments, bool isAsync, string tracerFactoryName)
        {
            if (TracerArgument.IsFlagSet(tracerArguments, TracerFlags.AttributeInstrumentation))
            {
                if (TracerArgument.IsFlagSet(tracerArguments, TracerFlags.WebTransaction) ||
                    TracerArgument.IsFlagSet(tracerArguments, TracerFlags.OtherTransaction))
                {
                    return isAsync ?
                        "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.OtherTransactionWrapperAsync" :
                        "NewRelic.Providers.Wrapper.CustomInstrumentation.OtherTransactionWrapper";
                }
            }

            return tracerFactoryName;
        }

        private void HandleBeforeWrappedMethodException(ulong functionId, TrackedWrapper trackedWrapper, InstrumentedMethodCall instrumentedMethodCall, InstrumentedMethodInfo instrumetedMethodInfo)
        {
            trackedWrapper.NoticeFailure();

            if (trackedWrapper.NumberOfConsecutiveFailures >= _maxConsecutiveFailures)
            {
                _agentHealthReporter.ReportWrapperShutdown(trackedWrapper.Wrapper, instrumentedMethodCall.MethodCall.Method);
                _functionIdToWrapper[functionId] = new InstrumentedMethodInfoWrapper(instrumetedMethodInfo, _wrapperMap.GetNoOpWrapper());
            }
        }

        private void GenerateSupportabilityMetrics(InstrumentedMethodInfo instrumentedMethodInfo, bool isCustom)
        {
            try
            {
                var reflectionAssemblyName = instrumentedMethodInfo.Method.Type.Assembly.GetName();
                var assemblyName = reflectionAssemblyName.Name;
                var assemblyVersion = reflectionAssemblyName.Version.ToString();

                _agentHealthReporter.ReportLibraryVersion(assemblyName, assemblyVersion);
                if (isCustom)
                {
                    string method = instrumentedMethodInfo.Method.MethodName;
                    string className = instrumentedMethodInfo.Method.Type.FullName;
                    _agentHealthReporter.ReportCustomInstrumentation(assemblyName, className, method);
                }    
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to generate Supportability Metrics for {instrumentedMethodInfo}");
            }
        }
    }
}
