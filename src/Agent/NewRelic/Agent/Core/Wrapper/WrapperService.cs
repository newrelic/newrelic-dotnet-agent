// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.ContinuousProfiling;
using NewRelic.Agent.Core.Tracer;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper;

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

                // Continuous profiling correlation: on the executing app thread, record this transaction's
                // trace/span in the native profiler so CPU samples taken on this thread link to it. Gated on a
                // single field read (IsEnabled) so it costs nothing when continuous profiling is off.
                PushContinuousProfilingContext(transaction);

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
                        finally
                        {
                            // Re-push the now-current context (the wrapped call ended, so CurrentSegment has
                            // popped back to the parent). This keeps the native TLS tracking the segment that is
                            // actually executing on this thread rather than leaving it pointing at the child.
                            // Gate the _agent.CurrentTransaction lookup itself behind IsEnabled: that argument is
                            // evaluated before PushContinuousProfilingContext runs, so without this check every
                            // instrumented method completion would pay for a transaction-context lookup even when
                            // continuous profiling is off.
                            if (ContinuousProfilingContext.Instance.IsEnabled)
                            {
                                PushContinuousProfilingContext(_agent.CurrentTransaction);
                            }
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

    /// <summary>
    /// Records the given transaction's current trace/span in the native continuous profiler, keyed by the
    /// calling (application) thread. Cheap no-op when continuous profiling is disabled (a single field read).
    /// The <see cref="IContinuousProfilingContext"/> swallows native failures; this method additionally guards
    /// the id extraction so nothing here can surface in the instrumented application.
    /// </summary>
    private static void PushContinuousProfilingContext(ITransaction transaction)
    {
        var context = ContinuousProfilingContext.Instance;
        if (!context.IsEnabled)
            return;

        try
        {
            // TraceId lives on the internal transaction; the public wrapper surface (ITransaction) doesn't expose it.
            var traceId = (transaction as IInternalTransaction)?.TraceId;
            var currentSegment = transaction.CurrentSegment;
            var spanId = (currentSegment != null && currentSegment.IsValid) ? currentSegment.SpanId : null;

            context.PushTraceContext(traceId, spanId);
        }
        catch (Exception ex)
        {
            Log.Finest(ex, "[ContinuousProfiling] Failed to read the current trace context for the native profiler.");
        }
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
