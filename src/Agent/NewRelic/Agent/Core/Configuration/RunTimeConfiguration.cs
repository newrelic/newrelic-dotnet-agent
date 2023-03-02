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

        public Func<Exception, string> ErrorGroupCallback;

        public RunTimeConfiguration()
        {
            ApplicationNames = Enumerable.Empty<string>();
            ErrorGroupCallback = null;
        }

        public RunTimeConfiguration(IEnumerable<string> applicationNames, Func<Exception, string> errorGroupCallback)
        {
            ApplicationNames = applicationNames.ToList();
            ErrorGroupCallback = errorGroupCallback;
        }
    }
}
