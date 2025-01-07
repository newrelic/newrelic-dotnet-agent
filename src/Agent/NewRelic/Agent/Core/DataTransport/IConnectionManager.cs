// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface IConnectionManager : IDisposable
    {
        Task<T> SendDataRequestAsync<T>(string method, params object[] data);

        void AttemptAutoStart();
    }
}
