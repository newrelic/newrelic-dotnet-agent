// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net.Http;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    /// <summary>
    /// Abstraction of System.Net.HttpMethod
    /// </summary>
    public enum HttpRequestMethod
    {
        Get,
        Post,
        Put,
        Delete
    }

    public static class HttpMethodExtensions
    {
        public static HttpMethod ToHttpMethod(this HttpRequestMethod method)
        {
            switch (method)
            {
                case HttpRequestMethod.Get:
                    return HttpMethod.Get;
                case HttpRequestMethod.Post:
                    return HttpMethod.Post;
                case HttpRequestMethod.Put:
                    return HttpMethod.Put;
                case HttpRequestMethod.Delete:
                    return HttpMethod.Delete;
                default:
                    throw new ArgumentOutOfRangeException(nameof(method), method, null);
            }
        }
    }
}
