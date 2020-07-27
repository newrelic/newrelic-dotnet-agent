using System;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Providers.Wrapper.WrapperUtilities;

namespace NewRelic.Providers.Wrapper.CustomInstrumentationAsync
{
    public class CustomSegmentWrapperAsync : IWrapper
    {
        [NotNull]
        private static readonly String[] PossibleWrapperNames = {
            "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.CustomSegmentWrapperAsync",
        };

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            if (PossibleWrapperNames.Contains(instrumentedMethodInfo.RequestedWrapperName))
            {
                //LegacyPipeline is only a concern w/ .NET Framework
                return WrapperUtilities.WrapperUtils.LegacyAspPipelineIsPresent()
                    ? new CanWrapResponse(false, WrapperUtilities.WrapperUtils.LegacyAspPipelineNotSupportedMessage("custom", "custom", instrumentedMethodInfo.Method.MethodName))
                    : new CanWrapResponse(true);
            }
            else
            {
                return new CanWrapResponse(false);
            }
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

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
                transaction.NoticeError(new ArgumentException("The CustomSegmentWrapperAsync can only be applied to a method with a String parameter."));
                return Delegates.NoOp;
            }

            var segment = transaction.StartCustomSegment(instrumentedMethodCall.MethodCall, segmentName);

            return WrapperUtils.GetAsyncDelegateFor(agentWrapperApi, segment);
        }
    }
}
