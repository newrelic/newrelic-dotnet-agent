using System;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.CustomInstrumentation
{
    public class CustomSegmentWrapper : IWrapper
    {
        [NotNull]
        private static readonly String[] PossibleWrapperNames = {
            "NewRelic.Providers.Wrapper.CustomInstrumentation.CustomSegmentWrapper",

            // To support older custom instrumentation we need to also accept the old tracer factory name
            "NewRelic.Agent.Core.Tracer.Factories.CustomSegmentTracerFactory"
        };

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            var canWrap = PossibleWrapperNames.Contains(instrumentedMethodInfo.RequestedWrapperName);
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            // find the first string argument
            String segmentName = null;
            foreach (var argument in instrumentedMethodCall.MethodCall.MethodArguments)
            {
                segmentName = argument as String;
                if (segmentName != null)
                    break;
            }

            if (segmentName == null)
            {
                throw new ArgumentException("The CustomSegmentWrapper can only be applied to a method with a String parameter.");
            }

            var segment = transaction.StartCustomSegment(instrumentedMethodCall.MethodCall, segmentName);

            return Delegates.GetDelegateFor(segment);
        }
    }
}
