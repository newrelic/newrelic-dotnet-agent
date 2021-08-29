// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Api.Agent;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace NetCore3Collectible
{
    public class CollectibleInstrumented
    {
        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Dictionary<string, string> InsertDistributedTraceHeaders()
        {
            var headers = new Dictionary<string, string>();
            IAgent agent = NewRelic.Api.Agent.NewRelic.GetAgent();
            ITransaction currentTransaction = agent.CurrentTransaction;
            var tooMuchWorkToGetAtDataCallback = new Action<Dictionary<string, string>, string, string>((carrier, key, value) => { carrier[key] = value; });
            currentTransaction.InsertDistributedTraceHeaders(headers, tooMuchWorkToGetAtDataCallback);

            return headers;
        }
    }
}
