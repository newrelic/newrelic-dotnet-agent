// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.IO;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public class AgentLogString : AgentLogBase
    {
        private readonly string _log;

        public AgentLogString(string log)
        {
            _log = log;
        }

        public override IEnumerable<string> GetFileLines()
        {
            string line;
            using (var stringReader = new StringReader(_log))
                while ((line = stringReader.ReadLine()) != null)
                {
                    yield return line;
                }
        }
    }
}
