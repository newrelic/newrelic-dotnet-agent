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
        private static IAgent _agent => NewRelic.Api.Agent.NewRelic.GetAgent();

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Dictionary<string, string> InsertDistributedTraceHeaders()
        {
            var headers = new Dictionary<string, string>();
            _agent.CurrentTransaction.InsertDistributedTraceHeaders(headers, NrApiHelpers.DictionaryInserter);
            return headers;
        }
    }

    /// <summary>
    /// Riffing on some helpers we might want to provide for the DT API in the future
    /// </summary>
    internal class NrApiHelpers
    {
        public static Action<Dictionary<string, string>, string, string> DictionaryInserter
            => (dictionary, key, value) => { dictionary[key] = value; };

    }
}
