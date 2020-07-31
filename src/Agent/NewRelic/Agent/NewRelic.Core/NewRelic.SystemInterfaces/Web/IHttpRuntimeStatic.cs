// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.SystemInterfaces.Web
{
    public interface IHttpRuntimeStatic
    {
        string AppDomainAppVirtualPath { get; }
    }
}
