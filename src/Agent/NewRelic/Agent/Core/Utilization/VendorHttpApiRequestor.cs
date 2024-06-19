// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Extensions.Logging;

#if NETFRAMEWORK
using System.IO;
using System.Net;
#else
using System.Net.Http;
#endif

namespace NewRelic.Agent.Core.Utilization
{
    public class VendorHttpApiRequestor
    {
        private const int WebReqeustTimeout = 1000;

        public virtual string CallVendorApi(Uri uri, string method, string vendorName, IEnumerable<string> headers = null)
        {
#if NETFRAMEWORK
            return CallWithWebRequest(uri, method, vendorName, headers);
#else
            return CallWithHttpClient(uri, method, vendorName, headers);
#endif
        }

#if NETFRAMEWORK
        private string CallWithWebRequest(Uri uri, string method, string vendorName, IEnumerable<string> headers = null)
        {
            try
            {
                var request = WebRequest.Create(uri);
                request.Method = method;
                request.Timeout = WebReqeustTimeout;

                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.Add(header);
                    }
                }

                using (var response = request.GetResponse() as HttpWebResponse)
                {
                    var stream = response?.GetResponseStream();
                    if (stream == null)
                        return null;

                    var reader = new StreamReader(stream);

                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                if (ex is WebException webEx)
                {
                    var response = (HttpWebResponse)webEx.Response;
                    if (response != null)
                    {
                        var statusCode = response.StatusCode.ToString() ?? string.Empty;
                        var statusDescription = response.StatusDescription ?? string.Empty;
                        // intentionally not passing the exception parameter here
                        Log.Debug("CallVendorApi ({0}) failed with WebException with status: {1}; message: {2}", vendorName, statusCode, statusDescription);
                    }
                    else
                    {
                        // intentionally not passing the exception parameter here
                        Log.Debug($"CallVendorApi ({{0}}) failed: {ex.Message}", vendorName);
                    }
                }
                else
                {
                    // intentionally not passing the exception parameter here
                    Log.Debug($"CallVendorApi ({{0}}) failed: {ex.Message}", vendorName);
                }
                return null;
            }
        }
#else
        private static readonly Lazy<HttpClient> httpClient = new Lazy<HttpClient>(() =>
            new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(WebReqeustTimeout)
            });


        private string CallWithHttpClient(Uri uri, string method, string vendorName, IEnumerable<string> headers = null)
        {
            try
            {
                var request = new HttpRequestMessage
                {
                    RequestUri = uri,
                    Method = new HttpMethod(method)
                };

                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        var separatorIndex = header.IndexOf(": ");
                        if (separatorIndex > -1)
                        {
                            var headerName = header.Substring(0, separatorIndex).Trim();
                            var headerValue = header.Substring(separatorIndex + 2).Trim(); // +2 to skip past ": "
                            request.Headers.TryAddWithoutValidation(headerName, headerValue);
                        }
                    }
                }

                var response = httpClient.Value.SendAsync(request).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    var statusCode = response.StatusCode;
                    var statusDescription = response.ReasonPhrase ?? string.Empty;
                    // intentionally not passing the exception parameter here
                    Log.Debug("CallVendorApi ({0}) failed with WebException with status: {1}; message: {2}", vendorName, statusCode, statusDescription);
                }

                return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // intentionally not passing the exception parameter here
                Log.Debug($"CallVendorApi ({{0}}) failed: {ex.Message}", vendorName);
                return null;
            }
        }
#endif
    }
}
