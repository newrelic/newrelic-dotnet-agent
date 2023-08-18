// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.DataTransport.Client.Interfaces
{
    /// <summary>
    /// Abstraction of a client request
    /// </summary>
    public interface IHttpRequest
    {
        IConnectionInfo ConnectionInfo { get; set; }
        string Endpoint { get; set; }
        Dictionary<string, string> Headers { get; }

        Uri Uri { get; }

        IHttpContent Content { get; }

        Guid RequestGuid { get; set; }
    }
}
