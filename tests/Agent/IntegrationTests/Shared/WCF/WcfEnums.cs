// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
namespace NewRelic.Agent.IntegrationTests.Shared.Wcf
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
#endif
