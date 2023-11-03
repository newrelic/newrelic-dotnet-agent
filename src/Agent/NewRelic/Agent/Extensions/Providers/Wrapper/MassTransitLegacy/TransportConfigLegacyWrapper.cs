// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using MassTransit;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.MassTransitLegacy
{
    public class TransportConfigLegacyWrapper : IWrapper
    {
        private const string WrapperName = "TransportConfigLegacyWrapper";
        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, Agent.Api.IAgent agent, ITransaction transaction)
        {
            // This will be run for each bus.  Each bus gets one transport.
            // We can support more than one transport with this setup.
            // Note that there are two instrumentation points that can get us here, and the first parameter is different for both,
            // but they both implement IBusFactoryConfigurator, which is what we need
            var configurator = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<IBusFactoryConfigurator>(0);

            var spec = new NewRelicPipeSpecification(agent);

            configurator.ConfigurePublish(cfg => cfg.AddPipeSpecification(spec));
            configurator.ConfigureSend(cfg => cfg.AddPipeSpecification(spec));
            configurator.AddPipeSpecification(spec);

            return Delegates.NoOp;
        }
    }
}
