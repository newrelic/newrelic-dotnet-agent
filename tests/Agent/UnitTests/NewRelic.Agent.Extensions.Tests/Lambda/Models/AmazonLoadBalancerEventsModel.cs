// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Mock.Amazon.Lambda.ApplicationLoadBalancerEvents
{

    public class RequestContextElb
    {
        public string TargetGroupArn { get; set; }
    }

    public class RequestContext
    {
        public RequestContextElb Elb { get; set; }
    }

    public class ApplicationLoadBalancerRequest
    {
        public RequestContext RequestContext { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public Dictionary<string, IList<string>> MultiValueHeaders {get; set;}
        public string HttpMethod { get; set; }
        public string Path { get; set; }
        public Dictionary<string, string> QueryStringParameters { get; set; }
    }
}
