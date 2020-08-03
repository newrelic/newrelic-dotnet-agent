// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface ICollectorWireFactory
    {
        ICollectorWire GetCollectorWire(IConfiguration configuration);
    }
}
