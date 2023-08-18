// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    /// <summary>
    /// Abstraction of a client request
    /// </summary>
    public class HttpRequest : IHttpRequest
    {
        private readonly IConfiguration _configuration;
        private Uri _uri;

        public HttpRequest(IConfiguration configuration)
        {
            _configuration = configuration;
            Content = new NRHttpContent(_configuration);
        }

        public IConnectionInfo ConnectionInfo { get; set; }
        public string Endpoint { get; set; }
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();
        public Uri Uri => _uri ??= GetUri(Endpoint, ConnectionInfo); // cache the Uri

        public IHttpContent Content { get; }
        public Guid RequestGuid { get; set; }

        private Uri GetUri(string method, IConnectionInfo connectionInfo)
        {
            var uri = new StringBuilder("/agent_listener/invoke_raw_method?method=")
                .Append(method)
                .Append($"&{Constants.LicenseKeyParameterName}=")
                .Append(_configuration.AgentLicenseKey)
                .Append("&marshal_format=json")
                .Append("&protocol_version=")
                .Append(Constants.ProtocolVersion);

            if (_configuration.AgentRunId != null)
            {
                uri.Append("&run_id=").Append(_configuration.AgentRunId);
            }

            var uriBuilder = new UriBuilder(connectionInfo.HttpProtocol, connectionInfo.Host, connectionInfo.Port, uri.ToString());

            return new Uri(uriBuilder.Uri.ToString().Replace("%3F", "?"));
        }
    }
}
