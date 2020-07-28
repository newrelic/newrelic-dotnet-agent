using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Core.Wrapper;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Providers.Wrapper.WrapperUtilities;

namespace NewRelic.Providers.Wrapper.CustomInstrumentationAsync
{
    public class DefaultWrapperAsync : IDefaultWrapper
    {
        private static readonly string[] PossibleWrapperNames = {
            "NewRelic.Agent.Core.Wrapper.DefaultWrapper",
            "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync",

            // To support older custom instrumentation we need to also accept the old tracer factory name
            "NewRelic.Agent.Core.Tracer.Factories.DefaultTracerFactory"
        };

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            var canWrap = instrumentedMethodInfo.IsAsync
                && PossibleWrapperNames.Contains(instrumentedMethodInfo.RequestedWrapperName);

            if (!canWrap)
            {
                return new CanWrapResponse(false);
            }

            //LegacyPipeline is only a concern w/ .NET Framework
            return WrapperUtilities.WrapperUtils.LegacyAspPipelineIsPresent() ?
                        new CanWrapResponse(false, WrapperUtilities.WrapperUtils.LegacyAspPipelineNotSupportedMessage("custom", "custom", instrumentedMethodInfo.Method.MethodName)) :
                        new CanWrapResponse(true);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            var typeName = instrumentedMethodCall.MethodCall.Method.Type.FullName ?? "<unknown>";
            var methodName = instrumentedMethodCall.MethodCall.Method.MethodName;
            var segment = !String.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName)
                ? transaction.StartCustomSegment(instrumentedMethodCall.MethodCall, instrumentedMethodCall.RequestedMetricName)
                : transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, typeName, methodName);

            if (!String.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName) && instrumentedMethodCall.RequestedTransactionNamePriority.HasValue)
            {
                transaction.SetCustomTransactionName(instrumentedMethodCall.RequestedMetricName, instrumentedMethodCall.RequestedTransactionNamePriority.Value);
            }

            return WrapperUtils.GetAsyncDelegateFor(agentWrapperApi, segment);
        }
    }
}
