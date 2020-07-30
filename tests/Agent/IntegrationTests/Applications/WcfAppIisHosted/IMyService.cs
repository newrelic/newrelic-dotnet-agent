/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.ServiceModel;

namespace NewRelic.Agent.IntegrationTests.Applications.WcfAppIisHosted
{
    [ServiceContract]
    public interface IMyService
    {
        [OperationContract]
        string GetData(int value);

        [OperationContract]
        string IgnoredTransaction(string input);

        [OperationContract]
        void ThrowException();
    }
}
