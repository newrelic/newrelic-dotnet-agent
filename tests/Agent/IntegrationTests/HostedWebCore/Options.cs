// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using CommandLine;

namespace HostedWebCore
{
    internal class Options
    {
        [Option("port", Required = true)]
        public string Port { get; set; }
    }
}
