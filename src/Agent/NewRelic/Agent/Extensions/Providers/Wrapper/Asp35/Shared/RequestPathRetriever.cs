/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Web;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Asp35.Shared
{
    public static class RequestPathRetriever
    {
        // In System.Web v4.0 (.NET 4 and up), accessing the Request.Path property directly clears a validation flag.  To prevent users from missing 
        // the validation warning, this function instead accesses the backing method.  If it's unavailable, it's safe to assume the System.Web version 
        // is 2.0, and can get Request.Path directly.

        private static readonly Func<HttpRequest, string> GetUnvalidatedRequestPath;

        static RequestPathRetriever()
        {
            try
            {
                GetUnvalidatedRequestPath = VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<HttpRequest, string>("GetUnvalidatedPath");
            }
            catch
            {
                GetUnvalidatedRequestPath = null;
            }
        }

        public static string TryGetRequestPath(HttpRequest request)
        {
            try
            {
                return GetUnvalidatedRequestPath?.Invoke(request) ?? request.Path;
            }
            catch
            {
                return null;
            }
        }
    }
}
