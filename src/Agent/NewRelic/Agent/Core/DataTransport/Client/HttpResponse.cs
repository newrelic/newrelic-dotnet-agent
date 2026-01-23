// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.DataTransport.Client;

/// <summary>
/// A response to a request by NrHttpClient
/// </summary>
public class HttpResponse : IHttpResponse
{
    private readonly IHttpResponseMessageWrapper _httpResponseMessageWrapper;

    private readonly Guid _requestGuid;

    public HttpResponse(Guid requestGuid, IHttpResponseMessageWrapper httpResponseMessageWrapper)
    {
        _requestGuid = requestGuid;
        _httpResponseMessageWrapper = httpResponseMessageWrapper;
    }

    public string GetContent()
    {
        try
        {
            if (_httpResponseMessageWrapper.Content == null)
            {
                return Constants.EmptyResponseBody;
            }

            var responseStream = _httpResponseMessageWrapper.Content.ReadAsStream();

            var contentTypeEncoding = _httpResponseMessageWrapper.Content.Headers.ContentEncoding;
            if (contentTypeEncoding.Contains("gzip"))
            {
                responseStream = new GZipStream(responseStream, CompressionMode.Decompress);
            }

            using (responseStream)
            using (var reader = new StreamReader(responseStream, Encoding.UTF8))
            {
                var responseBody = reader.ReadLineAsync().ConfigureAwait(false).GetAwaiter().GetResult();

                if (responseBody != null)
                {
                    return responseBody;
                }

                return Constants.EmptyResponseBody;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Request({0}): Unable to parse response body.", _requestGuid);
            return Constants.EmptyResponseBody;
        }
    }

    public bool IsSuccessStatusCode => _httpResponseMessageWrapper.IsSuccessStatusCode;
    public HttpStatusCode StatusCode => _httpResponseMessageWrapper.StatusCode;

    public void Dispose()
    {
        _httpResponseMessageWrapper?.Dispose();
    }
}
#endif
