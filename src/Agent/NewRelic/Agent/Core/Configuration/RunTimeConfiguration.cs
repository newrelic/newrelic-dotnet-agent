// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.Configuration
{
    public class RunTimeConfiguration
    {
        public IEnumerable<string> ApplicationNames;

        public Func<IReadOnlyDictionary<string, object>, string> ErrorGroupCallback;

        public Func<string, string, int> LlmTokenCountingCallback;

        public RunTimeConfiguration()
        {
            ApplicationNames = Enumerable.Empty<string>();
            ErrorGroupCallback = null;
            LlmTokenCountingCallback = null;
        }

        public RunTimeConfiguration(IEnumerable<string> applicationNames, Func<IReadOnlyDictionary<string, object>, string> errorGroupCallback, Func<string, string, int> llmTokenCountingCallback)
        {
            ApplicationNames = applicationNames.ToList();
            ErrorGroupCallback = errorGroupCallback;
            LlmTokenCountingCallback = llmTokenCountingCallback;
        }
    }
}
