/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.DataTransport
{
    public class HttpCollectorWireFactory : ICollectorWireFactory
    {
        public ICollectorWire GetCollectorWire(IConfiguration configuration)
        {
            return new HttpCollectorWire(configuration);
        }
    }
}
