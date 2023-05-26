// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading;
using NewRelic.Core.CodeAttributes;
using Serilog.Core;
using Serilog.Events;

namespace NewRelic.Agent.Core
{
    [NrExcludeFromCodeCoverage]
    internal class ThreadIdEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "tid", Thread.CurrentThread.ManagedThreadId));
        }
    }
}
