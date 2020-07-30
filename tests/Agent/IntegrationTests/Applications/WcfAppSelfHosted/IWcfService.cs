/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.ServiceModel;

namespace NewRelic.Agent.IntegrationTests.Applications.WcfAppSelfHosted
{
    [ServiceContract]
    public interface IWcfService
    {
        [OperationContract]
        string GetString();

        [OperationContract]
        string ReturnString(string input);

        [OperationContract]
        void ThrowException();

        [OperationContract]
        string IgnoredTransaction(string input);
    }
}
