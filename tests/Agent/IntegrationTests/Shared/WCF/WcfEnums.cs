// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.IntegrationTests.Shared.Wcf
{
    public enum WCFBindingType
    {
        BasicHttp,
        WSHttp,
        WSHttpUnsecure,
        WebHttp,
        NetTcp,
        Custom,
        CustomClass
    }

    public enum HostingModel
    {
        Self,
        IIS,
        IISNoAsp
    }

    public enum WCFInvocationMethod
    {
        Sync,
        BeginEndAsync,
        TAPAsync,
        EventBasedAsync
    }
}
