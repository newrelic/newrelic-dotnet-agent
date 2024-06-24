// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AwsSdk
{
    public static class SqsHelper
    {
        public const string VendorName = "SQS";

        private class SqsAttributes
        {
            public string QueueName;
            public string CloudId;
            public string Region;

            // https://sqs.us-east-2.amazonaws.com/123456789012/MyQueue
            public SqsAttributes(string url)
            {
                var parts = url.Split('/');
                var subdomain = parts[2].Split('.');
                // subdomain[0] should always be "sqs"
                Region = subdomain[1];
                CloudId = parts[3];
                QueueName = parts[4];
            }
        }
        public static ISegment GenerateSegment(ITransaction transaction, MethodCall methodCall, string url, MessageBrokerAction action)
        {
            var attr = new SqsAttributes(url);
            return transaction.StartMessageBrokerSegment(methodCall, MessageBrokerDestinationType.Queue, action, VendorName, attr.QueueName);
        }

        public static void InsertDistributedTraceHeaders(ITransaction transaction, dynamic webRequest)
        {
            var setHeaders = new Action<dynamic, string, string>((wr, key, value) =>
            {
                var headers = wr.Headers as IDictionary<string, object>;

                if (headers == null)
                {
                    headers = new Dictionary<string, object>();
                    wr.Headers = headers;
                }

                headers[key] = value;
            });

            transaction.InsertDistributedTraceHeaders(webRequest, setHeaders);

        }
    }
}
