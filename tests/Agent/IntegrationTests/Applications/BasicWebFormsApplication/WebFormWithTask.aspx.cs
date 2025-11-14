// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace BasicWebFormsApplication
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD103:Call async methods when in an async method", Justification = "Intentional sync-over-async for test scenario.")]
    public partial class WebFormWithTask : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var task = Task.Run(ExternalCallAndReturnString);
            Task.WaitAll(task);

            var foo = task.Result;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD103:Call async methods when in an async method", Justification = "Intentional sync-over-async for test scenario.")]
        private Task<string> ExternalCallAndReturnString()
        {
            // make an http web request to example.com and await the response
            using (var httpClient = new HttpClient())
            {
                var response = httpClient.GetStringAsync("https://google.com").Result;
                return Task.FromResult(response);
            }
        }
    }
}
