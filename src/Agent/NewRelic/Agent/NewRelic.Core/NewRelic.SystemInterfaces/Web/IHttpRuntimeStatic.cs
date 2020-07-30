/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace NewRelic.SystemInterfaces.Web
{
    public interface IHttpRuntimeStatic
    {
        string AppDomainAppVirtualPath { get; }
    }
}
