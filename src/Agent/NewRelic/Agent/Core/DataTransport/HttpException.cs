// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net;

namespace NewRelic.Agent.Core.DataTransport
{
    /// <summary>
    /// Thrown when the connection to the collector(RPM) reports an HTTP transport error.
    /// </summary>
    public class HttpException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public HttpException(HttpStatusCode statusCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
