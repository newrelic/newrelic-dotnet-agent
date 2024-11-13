// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AzureServiceBus
{
    public class AzureServiceBusReceiverManagerWrapper : AzureServiceBusWrapperBase
    {
        public override bool IsTransactionRequired => false;

        public override CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            var canWrap = instrumentedMethodInfo.RequestedWrapperName.Equals(nameof(AzureServiceBusReceiverManagerWrapper));
            return new CanWrapResponse(canWrap);
        }

        public override AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent,
            ITransaction transaction)
        {
            // TODO not working at present -- transaction is always NoOpTransaction here but shouldn't be 
            // make sure the transaction ends when the receiver manager is done processing messages
            if (instrumentedMethodCall.IsAsync)
            {
                return Delegates.GetAsyncDelegateFor<Task>(
                    agent,
                    agent.CurrentTransaction.CurrentSegment,
                    false,
                    onComplete: _ =>
                    {
                        agent.CurrentTransaction.End();
                    });
            }
            return Delegates.GetDelegateFor(onComplete: () =>
            {
                agent.CurrentTransaction.End();
            });
        }
    }
}
