// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Mock.Amazon.Lambda.ApplicationLoadBalancerEvents
{
    // https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.ApplicationLoadBalancerEvents/ApplicationLoadBalancerRequest.cs
    public class ApplicationLoadBalancerRequest
    {
        public ALBRequestContext RequestContext { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public Dictionary<string, IList<string>> MultiValueHeaders {get; set;}
        public string HttpMethod { get; set; }
        public string Path { get; set; }
        public Dictionary<string, string> QueryStringParameters { get; set; }

        public class ElbInfo
        {
            public string TargetGroupArn { get; set; }
        }

        public class ALBRequestContext
        {
            public ElbInfo Elb { get; set; }
        }
    }
}
