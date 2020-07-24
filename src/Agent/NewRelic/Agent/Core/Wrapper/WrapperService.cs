using System;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Tracer;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Collections;

namespace NewRelic.Agent.Core.Wrapper
{
    public interface IWrapperService
    {
        [CanBeNull]
        AfterWrappedMethodDelegate BeforeWrappedMethod([NotNull] Type type, [NotNull] String methodName, [NotNull] String argumentSignature, [CanBeNull] Object invocationTarget, [NotNull] Object[] methodArguments, [CanBeNull] String tracerFactoryName, [CanBeNull] String metricName, [NotNull] uint tracerArguments, UInt64 functionId);
    }

    public class WrapperService : IWrapperService
    {
        private readonly Int32 _maxConsecutiveFailures;

        [NotNull] private readonly IConfigurationService _configurationService;

        [NotNull] private readonly IAgentWrapperApi _agentWrapperApi;

        [NotNull] private readonly IWrapperMap _wrapperMap;

        [NotNull] private readonly IAgentHealthReporter _agentHealthReporter;

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

        [NotNull] private readonly ConcurrentDictionary<UInt64, InstrumentedMethodInfoWrapper> _functionIdToWrapper;

        public WrapperService([NotNull] IConfigurationService configurationService, [NotNull] IWrapperMap wrapperMap,
            [NotNull] IAgentWrapperApi agentWrapperApi, [NotNull] IAgentHealthReporter agentHealthReporter)
        {
            _configurationService = configurationService;
            _maxConsecutiveFailures = configurationService.Configuration.WrapperExceptionLimit;
            _agentWrapperApi = agentWrapperApi;
            _wrapperMap = wrapperMap;
            _agentHealthReporter = agentHealthReporter;
            _functionIdToWrapper = new ConcurrentDictionary<ulong, InstrumentedMethodInfoWrapper>();
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(Type type, String methodName, String argumentSignature,
            Object invocationTarget, Object[] methodArguments, String tracerFactoryName, String metricName,
            uint tracerArguments, UInt64 functionId)
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
                var isAsync = TracerArgument.IsAsync(tracerArguments);

                tracerFactoryName = ResolveTracerFactoryNameForAttributeInstrumentation(tracerArguments, isAsync, tracerFactoryName);

                var method = new Method(type, methodName, argumentSignature, functionId.GetHashCode());
                var transactionNamePriority = TracerArgument.GetTransactionNamingPriority(tracerArguments);
                instrumentedMethodInfo = new InstrumentedMethodInfo((long)functionId, method, tracerFactoryName, isAsync, metricName, transactionNamePriority, TracerArgument.IsFlagSet(tracerArguments, TracerFlags.WebTransaction));

                trackedWrapper = _wrapperMap.Get(instrumentedMethodInfo);

                if (trackedWrapper == null)
                {
                    Log.WarnFormat("WrapperMap.Get unexpectedly returned null for {0}.{1}({2}) in assembly [{3}] (requested wrapper name was {4}).",
                        instrumentedMethodInfo.Method.Type.FullName,
                        instrumentedMethodInfo.Method.MethodName,
                        instrumentedMethodInfo.Method.ParameterTypeNames,
                        instrumentedMethodInfo.Method.Type.Assembly.FullName,
                        instrumentedMethodInfo.RequestedWrapperName);

                    return null;
                }

                _functionIdToWrapper[functionId] = new InstrumentedMethodInfoWrapper(instrumentedMethodInfo, trackedWrapper);
            }

            var wrapper = trackedWrapper.Wrapper;

            ITransaction transaction = null;
            if (wrapper.IsTransactionRequired)
            {
                transaction = _agentWrapperApi.CurrentTransaction;
                if (!transaction.IsValid)
                {
                    Log.FinestFormat("No transaction, skipping method {0}.{1}({2})", type.FullName, methodName, argumentSignature);
                    return Delegates.NoOp;
                }
            }

            var methodCall = new MethodCall(instrumentedMethodInfo.Method, invocationTarget, methodArguments);
            var instrumentedMethodCall = new InstrumentedMethodCall(methodCall, instrumentedMethodInfo);

            // if the wrapper throws an exception when executing the pre-method code, make sure the wrapper isn't called again in the future
            try
            {
                var afterWrappedMethod = wrapper.BeforeWrappedMethod(instrumentedMethodCall, _agentWrapperApi, transaction);
                return (result, exception) =>
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
                };
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

        private void HandleBeforeWrappedMethodException(UInt64 functionId, TrackedWrapper trackedWrapper, InstrumentedMethodCall instrumentedMethodCall, InstrumentedMethodInfo instrumetedMethodInfo)
        {
            trackedWrapper.NoticeFailure();

            if (trackedWrapper.NumberOfConsecutiveFailures >= _maxConsecutiveFailures)
            {
                _agentHealthReporter.ReportWrapperShutdown(trackedWrapper.Wrapper, instrumentedMethodCall.MethodCall.Method);
                _functionIdToWrapper[functionId] = new InstrumentedMethodInfoWrapper(instrumetedMethodInfo, _wrapperMap.GetNoOpWrapper());
            }
        }
    }
}
