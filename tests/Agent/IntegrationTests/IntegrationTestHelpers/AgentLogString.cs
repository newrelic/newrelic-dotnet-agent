// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public class AgentLogString : AgentLogBase
    {
        private readonly string _logContents;

        public AgentLogString(string logContents)
            : base(null)
        {
            _logContents = logContents;
        }

        public override IEnumerable<string> GetFileLines()
        {
            return _logContents.Split('\n');
        }
    }
}
