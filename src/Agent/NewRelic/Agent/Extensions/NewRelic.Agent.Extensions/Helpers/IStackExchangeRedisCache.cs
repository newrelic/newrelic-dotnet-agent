﻿// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Extensions.Helpers
{
    /// <summary>
    /// Interface that allows the agent Core to hold a reference to the SessionCache which the instrumentation creates,
    /// but not have the Agent Core depend on the StackExchange.Redis packages.
    /// </summary>
    public interface IStackExchangeRedisCache : IDisposable
    {
        void Harvest(string spanId);

        int Count { get; }
    }
}
