// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface ICollectorWire
    {
        Task<string> SendDataAsync(string method, ConnectionInfo connectionInfo, string serializedData, Guid requestGuid);
    }
}
