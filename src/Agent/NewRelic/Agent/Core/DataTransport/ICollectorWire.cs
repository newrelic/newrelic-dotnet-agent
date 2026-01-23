// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.DataTransport;

public interface ICollectorWire
{
    string SendData(string method, ConnectionInfo connectionInfo, string serializedData, Guid requestGuid);
}