// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Mock.Amazon.Lambda.APIGatewayEvents
{
    public class APIGatewayProxyRequest
    {
        public RequestContext RequestContext { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public Dictionary<string, IList<string>> MultiValueHeaders {get; set;}
        public string HttpMethod { get; set; }
        public string Path { get; set; }
        public Dictionary<string, string> QueryStringParameters { get; set; }
    }

    public class RequestContext
    {
        public string AccountId { get; set; }
        public string ApiId { get; set; }
        public string ResourceId { get; set; }
        public string ResourcePath { get; set; }
        public string Stage { get; set; }
    }
}
