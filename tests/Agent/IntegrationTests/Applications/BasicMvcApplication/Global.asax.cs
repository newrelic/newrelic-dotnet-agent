// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Net;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Http;

namespace BasicMvcApplication
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            OverrideSslSettingsForMockNewRelic();
        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            if (Request.Path.Contains("HandleThisRequestInGlobalAsax"))
            {
                var hasStatusCode = int.TryParse(Request.QueryString["statusCode"], out var statusCode);
                if (hasStatusCode)
                {
                    Response.StatusCode = statusCode;
                }

                Response.End();
            }
        }

        /// <summary>
        /// When the MockNewRelic app is used in place of the normal New Relic / Collector endpoints,
        /// the mock version uses a self-signed cert that will not be "trusted."
        /// 
        /// This forces all validation checks to pass.
        /// </summary>
        private static void OverrideSslSettingsForMockNewRelic()
        {
#if !NET10_0_OR_GREATER
            ServicePointManager.ServerCertificateValidationCallback = delegate
            {
                //force trust on all certificates for simplicity
                return true;
            };
#endif
        }
    }
}
