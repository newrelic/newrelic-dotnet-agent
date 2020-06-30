/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

namespace NewRelic.Agent.IntegrationTestHelpers.ApplicationLibraries.Wcf
{
    public enum WCFBindingType
    {
        BasicHttp,
        WSHttp,
        WebHttp,
        NetTcp,
        Custom,
        CustomClass
    }

    public enum WCFInvocationMethod
    {
        Sync,
        BeginEndAsync,
        TAPAsync,
        EventBasedAsync
    }
}
