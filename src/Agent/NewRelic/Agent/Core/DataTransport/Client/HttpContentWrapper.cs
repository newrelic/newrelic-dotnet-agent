// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    /// <summary>
    /// HttpContent wrapper to enable mocking in unit tests
    /// </summary>
    public class HttpContentWrapper : IHttpContentWrapper
    {
        private readonly HttpContent _httpContent;

        public HttpContentWrapper(HttpContent httpContent)
        {
            _httpContent = httpContent;
        }

        public Task<Stream> ReadAsStreamAsync()
        {
            return _httpContent.ReadAsStreamAsync();
        }

        public IHttpContentHeadersWrapper Headers => new HttpContentHeadersWrapper(_httpContent.Headers);
    }
}
#endif
