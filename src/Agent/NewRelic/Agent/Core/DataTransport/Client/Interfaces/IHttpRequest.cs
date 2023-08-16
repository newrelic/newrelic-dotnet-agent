// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    public interface IHttpRequest
    {
        ConnectionInfo ConnectionInfo { get; set; }
        string Endpoint { get; set; }
        Dictionary<string, string> Headers { get; }
        HttpRequestMethod Method { get; set; }
        TimeSpan Timeout { get; set; }

        Uri Uri { get; }

        IHttpContent Content { get; }

        Guid RequestGuid { get; set; }
    }
}
