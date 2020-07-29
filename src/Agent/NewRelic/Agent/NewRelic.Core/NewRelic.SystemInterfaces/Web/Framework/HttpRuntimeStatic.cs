/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#if NET35
using System;

namespace NewRelic.SystemInterfaces.Web
{

    public class HttpRuntimeStatic : IHttpRuntimeStatic
    {
        public string AppDomainAppVirtualPath => System.Web.HttpRuntime.AppDomainAppVirtualPath;
    }
}
#endif
