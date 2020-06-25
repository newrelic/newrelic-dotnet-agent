/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace NewRelic.Agent.Core.DataTransport
{
    public interface IConnectionManager
    {
        T SendDataRequest<T>(string method, params object[] data);

        void AttemptAutoStart();
    }
}
