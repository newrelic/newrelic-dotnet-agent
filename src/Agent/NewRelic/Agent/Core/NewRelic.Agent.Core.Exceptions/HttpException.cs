/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Net;

namespace NewRelic.Agent.Core.Exceptions
{

    /// <summary>
    /// Thrown when the connection to the collector(RPM) reports an HTTP transport error.
    /// </summary>
    public class HttpException : RPMException
    {
        public HttpStatusCode StatusCode { get; private set; }

        public HttpException(HttpStatusCode statusCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
