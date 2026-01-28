// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETSTANDARD2_0

namespace NewRelic.Agent.Core.SharedInterfaces.Web.Core;

public class HttpRuntimeStatic : IHttpRuntimeStatic
{
    public string AppDomainAppVirtualPath => string.Empty; //Microsoft.AspNetCore.Http.AppDomainAppVirtualPath;
}
#endif
