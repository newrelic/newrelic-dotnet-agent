// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
#if NETFRAMEWORK
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    /// <summary>
    /// A response to a request by NRWebRequestClient
    /// </summary>
    public class WebRequestClientResponse : IHttpResponse
    {
        private readonly Guid _requestGuid;
        private readonly HttpWebResponse _response;

        public WebRequestClientResponse(Guid requestGuid, HttpWebResponse response)
        {
            _requestGuid = requestGuid;
            _response = response;
        }

        public Task<string> GetContentAsync()
        {
            try
            {
                var responseStream = _response.GetResponseStream();
                if (responseStream == null)
                {
                    throw new NullReferenceException("responseStream");
                }

                if (_response.Headers == null)
                {
                    throw new NullReferenceException("response.Headers");
                }

                var contentTypeEncoding = _response.Headers.Get("content-encoding");
                if ("gzip".Equals(contentTypeEncoding))
                {
                    responseStream = new GZipStream(responseStream, CompressionMode.Decompress);
                }

                using (responseStream)
                using (var reader = new StreamReader(responseStream, Encoding.UTF8))
                {
                    var responseBody = reader.ReadLine();
                    return Task.FromResult(responseBody ?? Constants.EmptyResponseBody);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Request({0}): Unable to parse response body.", _requestGuid);

                return Task.FromResult(Constants.EmptyResponseBody);
            }
        }

        public bool IsSuccessStatusCode => (200 <= (int)_response.StatusCode) && ((int)_response.StatusCode <= 299);
        public HttpStatusCode StatusCode => _response.StatusCode;

        public void Dispose()
        {
            _response?.Dispose();
        }
    }
}
#endif
