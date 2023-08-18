// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    /// <summary>
    /// Abstract base shared by implementations of IHttpClient
    /// </summary>
    public abstract class HttpClientBase : IHttpClient
    {
        protected bool _diagnoseConnectionError = true;
        protected static IWebProxy _proxy = null;

        protected HttpClientBase(IWebProxy proxy)
        {
            _proxy = proxy;
        }

        public abstract Task<IHttpResponse> SendAsync(IHttpRequest request);

        public virtual void Dispose()
        {
#if !NETFRAMEWORK
            if (_lazyHttpClient.IsValueCreated)
                _lazyHttpClient.Value.Dispose();
#endif
        }


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
#if NETFRAMEWORK
                using (var wc = new WebClient())
                {
                    wc.Proxy = _proxy;

                    wc.DownloadString(testAddress);
                }
#else
                _lazyHttpClient.Value.GetAsync(testAddress).GetAwaiter().GetResult();
#endif
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

#if !NETFRAMEWORK
        // use a single HttpClient for all TestConnection() invocations
        private readonly Lazy<HttpClient> _lazyHttpClient = new Lazy<HttpClient>(() => new HttpClient(new HttpClientHandler() { Proxy = _proxy }));
#endif
    }
}
