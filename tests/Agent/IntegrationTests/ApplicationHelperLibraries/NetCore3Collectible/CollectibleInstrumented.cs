// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Api.Agent;
using System.Runtime.CompilerServices;

namespace NetCore3Collectible
{
    public class CollectibleInstrumented
    {
        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public IDistributedTracePayload GetDistributedTracePayload()
        {
            return NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction?.CreateDistributedTracePayload();
        }
    }
}
