// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.Utilization
{
    public class VendorHttpApiRequestor
    {
        private const int WebReqeustTimeout = 1000;

        public virtual string CallVendorApi(Uri uri, string method, string vendorName, IEnumerable<string> headers = null)
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
                        Log.Debug(ex, "CallVendorApi ({0}) failed with WebException with status: {1}; message: {2}", vendorName, statusCode, statusDescription);
                    }
                    else
                    {
                        Log.Debug(ex, "CallVendorApi ({0}) failed", vendorName);
                    }
                }
                else
                {
                    Log.Debug(ex, "CallVendorApi ({0}) failed", vendorName);
                }
                return null;
            }
        }
    }
}
