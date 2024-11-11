// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AzureServiceBus
{
    public abstract class AzureServiceBusWrapperBase : IWrapper
    {
        protected const string BrokerVendorName = "AzureServiceBus";

        public bool IsTransactionRequired => true; // only instrument service bus methods if we're already in a transaction

        public abstract CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo);

        public abstract AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent,ITransaction transaction);

    }
}
