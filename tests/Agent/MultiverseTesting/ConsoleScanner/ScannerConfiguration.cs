// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;

namespace NewRelic.Agent.ConsoleScanner
{
    public class ScannerConfiguration
    {
        public List<InstrumentationSet> InstrumentationSets { get; set; }
    }
}
