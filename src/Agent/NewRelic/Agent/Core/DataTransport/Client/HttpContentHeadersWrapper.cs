// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    public class HttpContentHeadersWrapper : IHttpContentHeadersWrapper
    {
        private readonly HttpContentHeaders _headers;

        public HttpContentHeadersWrapper(HttpContentHeaders headers)
        {
            _headers = headers;
        }

        public ICollection<string> ContentEncoding => _headers.ContentEncoding.ToList();
    }
}
