// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.Events;

public class AppNameUpdateEvent
{
    public readonly IEnumerable<string> AppNames;

    public AppNameUpdateEvent(IEnumerable<string> appNames)
    {
        AppNames = appNames;
    }
}