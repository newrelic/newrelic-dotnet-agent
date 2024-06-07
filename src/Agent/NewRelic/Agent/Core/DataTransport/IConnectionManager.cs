// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface IConnectionManager : IDisposable
    {
        T SendDataRequest<T>(string method, params object[] data);

        void AttemptAutoStart();
    }
}
