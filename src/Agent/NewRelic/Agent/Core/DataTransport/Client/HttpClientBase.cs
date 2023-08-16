// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net;
using System.Threading.Tasks;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    public abstract class HttpClientBase : IHttpClient
    {
        protected bool _diagnoseConnectionError = true;
        protected IWebProxy _proxy;

        protected HttpClientBase(IWebProxy proxy)
        {
            _proxy = proxy;
        }

        public abstract Task<IHttpResponse> SendAsync(IHttpRequest request);
        public abstract void Dispose();


        protected void DiagnoseConnectionError(string host)
        {
            _diagnoseConnectionError = false;
            try
            {
                if (!IPAddress.TryParse(host, out _))
                {
                    Dns.GetHostEntry(host);
                }
            }
            catch (Exception)
            {
                Log.ErrorFormat("Unable to resolve host name \"{0}\"", host);
            }

            TestConnection();
        }

        protected void TestConnection()
        {
            const string testAddress = "http://www.google.com";
            try
            {
                using (var wc = new WebClient())
                {
                    wc.Proxy = _proxy;

                    wc.DownloadString(testAddress);
                }

                Log.InfoFormat("Connection test to \"{0}\" succeeded", testAddress);
            }
            catch (Exception)
            {
                var message = $"Connection test to \"{testAddress}\" failed.";
                if (_proxy != null)
                {
                    message += $" Check your proxy settings ({_proxy.GetProxy(new Uri(testAddress))})";
                }

                Log.Error(message);
            }
        }
    }
}
