// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
#if !NETFRAMEWORK
using System.Net;
using System.Net.Http;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    /// <summary>
    /// HttpResponseMessage wrapper to enable mocking in unit tests
    /// </summary>
    public class HttpResponseMessageWrapper : IHttpResponseMessageWrapper
    {
        private readonly HttpResponseMessage _responseMessage;

        public HttpResponseMessageWrapper(HttpResponseMessage responseMessage)
        {
            _responseMessage = responseMessage;
        }

        public IHttpContentWrapper Content => _responseMessage.Content == null ? null : new HttpContentWrapper(_responseMessage.Content);
        public bool IsSuccessStatusCode => _responseMessage.IsSuccessStatusCode;
        public HttpStatusCode StatusCode => _responseMessage.StatusCode;

        public void Dispose()
        {
            _responseMessage?.Dispose();
        }
    }
}
#endif
