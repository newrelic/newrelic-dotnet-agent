// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Extensions.AwsSdk
{
    public static class SqsHelper
    {
        public const string VendorName = "SQS";

        private class SqsAttributes
        {
            public string QueueName { get; }
            public string CloudId { get; }
            public string Region { get; }

            // https://sqs.us-east-2.amazonaws.com/123456789012/MyQueue
            public SqsAttributes(string url)
            {
                if (string.IsNullOrEmpty(url))
                {
                    return;
                }

                var parts = url.Split('/');
                if (parts.Length < 5)
                {
                    return;
                }

                CloudId = parts[3];
                QueueName = parts[4];

                var subdomain = parts[2].Split('.');
                if (subdomain.Length < 2)
                {
                    return;
                }

                // subdomain[0] should always be "sqs"
                Region = subdomain[1];
            }
        }
        public static ISegment GenerateSegment(ITransaction transaction, MethodCall methodCall, string url, MessageBrokerAction action)
        {
            var attr = new SqsAttributes(url);
            return transaction.StartMessageBrokerSegment(methodCall, MessageBrokerDestinationType.Queue, action, VendorName, attr.QueueName);
        }

        public static void InsertDistributedTraceHeaders(ITransaction transaction, dynamic sendMessageRequest)
        {
            var setHeaders = new Action<dynamic, string, string>((smr, key, value) =>
            {
                var headers = smr.MessageAttributes as IDictionary<string, object>;

                if (headers == null)
                {
                    headers = new Dictionary<string, object>();
                    smr.MessageAttributes = headers;
                }

                // this needs to be set to a MessageAttributeValue (??)
                headers[key] = value;
            });

            transaction.InsertDistributedTraceHeaders(sendMessageRequest, setHeaders);

        }
        public static void AcceptDistributedTraceHeaders(ITransaction transaction, dynamic sendMessageRequest)
        {
            var getHeaders = new Func<dynamic, string, IEnumerable<string>>((smr, key) =>
            {
                var returnValues = new List<string>();
                var headers = smr.MessageAttributes as IDictionary<string, object>;

                if (headers != null)
                {
                    foreach (var item in headers)
                    {
                        if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                        {
                            returnValues.Add(headers[key].ToString());
                        }
                    }
                    return returnValues;
                }

                return null;
            });

            // Do we want to define a new transport type for SQS?
            transaction.AcceptDistributedTraceHeaders(sendMessageRequest, getHeaders, TransportType.Queue);

        }
    }
}
