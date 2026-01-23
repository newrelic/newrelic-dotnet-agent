// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.DataTransport;

public class NoOpCollectorWire : ICollectorWire
{
    public string SendData(string method, ConnectionInfo connectionInfo, string serializedData, Guid requestGuid)
    {
        // Any valid JSON without an exception can be returned
        return "{}";
    }
}