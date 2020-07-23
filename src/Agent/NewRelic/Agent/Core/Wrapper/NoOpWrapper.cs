using System;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper
{
    public interface INoOpWrapper : IWrapper
    {
    }

    public class NoOpWrapper : INoOpWrapper
    {
        [NotNull]
        private static readonly String[] PossibleWrapperNames = {
            "NewRelic.Agent.Core.Wrapper.NoOpWrapper",
			// To support older custom instrumentation we need to also accept the old tracer factory name
			"NewRelic.AgentCore.Tracer.Factories.NoOpTracerFactory"
        };

        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            var canWrap = PossibleWrapperNames.Contains(instrumentedMethodInfo.RequestedWrapperName);
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            return Delegates.NoOp;
        }
    }
}
