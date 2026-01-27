// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DataTransport;

public interface ISerializer
{
    string Serialize(object[] parameters);
    T Deserialize<T>(string responseBody);
}