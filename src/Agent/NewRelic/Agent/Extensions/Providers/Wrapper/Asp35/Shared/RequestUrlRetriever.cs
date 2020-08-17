// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Web;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Asp35.Shared
{
    public static class RequestUrlRetriever
    {
        // In System.Web v4.0 (.NET 4 and up), accessing the Request.Url property directly clears a validation flag.  To prevent users from missing 
        // the validation warning, this function instead accesses the backing method.  If it's unavailable, it's safe to assume the System.Web version 
        // is 2.0, and can get Request.Url directly.

        private static readonly Func<HttpRequest, Func<string>, Uri> GetUnvalidatedRequestUrl;

        static RequestUrlRetriever()
        {
            try
            {
                GetUnvalidatedRequestUrl = VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<HttpRequest, Func<string>, Uri>("BuildUrl");
            }
            catch
            {
                GetUnvalidatedRequestUrl = null;
            }
        }

        public static Uri TryGetRequestUrl(HttpRequest request, Func<string> pathAccessor)
        {
            try
            {
                return GetUnvalidatedRequestUrl?.Invoke(request, pathAccessor) ?? request.Url;
            }
            catch
            {
                return null;
            }
        }
    }
}
