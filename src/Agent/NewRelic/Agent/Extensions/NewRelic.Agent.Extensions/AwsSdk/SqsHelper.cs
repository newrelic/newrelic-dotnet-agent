// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Agent.Extensions.AwsSdk
{
    public static class SqsHelper
    {
        private static Func<object, IDictionary> _getMessageAttributes;
        private static Func<object> _messageAttributeValueTypeFactory;

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
            var segment = transaction.StartMessageBrokerSegment(methodCall, MessageBrokerDestinationType.Queue, action, VendorName, attr.QueueName);
            segment.GetExperimentalApi().MakeLeaf();

            return segment;
        }

        public static void InsertDistributedTraceHeaders(ITransaction transaction, object sendMessageRequest)
        {
            var headersInserted = 0;

            var setHeaders = new Action<object, string, string>((smr, key, value) =>
            {
                var getMessageAttributes = _getMessageAttributes ??=
                    VisibilityBypasser.Instance.GeneratePropertyAccessor<IDictionary>(
                        smr.GetType(), "MessageAttributes");

                var messageAttributes = getMessageAttributes(smr);

                // SQS is limited to no more than 10 attributes; if we can't add up to 3 attributes, don't add any
                if ((messageAttributes.Count + 3 - headersInserted) > 10)
                    return;

                // create a new MessageAttributeValue instance
                var messageAttributeValueTypeFactory = _messageAttributeValueTypeFactory ??= VisibilityBypasser.Instance.GenerateTypeFactory(smr.GetType().Assembly.FullName, "Amazon.SQS.Model.MessageAttributeValue");
                object newMessageAttributeValue = messageAttributeValueTypeFactory.Invoke();

                var dataTypePropertySetter = VisibilityBypasser.Instance.GeneratePropertySetter<string>(newMessageAttributeValue, "DataType");
                dataTypePropertySetter("String");

                var stringValuePropertySetter = VisibilityBypasser.Instance.GeneratePropertySetter<string>(newMessageAttributeValue, "StringValue");
                stringValuePropertySetter(value);

                messageAttributes.Add(key, newMessageAttributeValue);

                ++headersInserted;
            });

            transaction.InsertDistributedTraceHeaders(sendMessageRequest, setHeaders);

        }
        public static void AcceptDistributedTraceHeaders(ITransaction transaction, dynamic messageAttributes)
        {
            var getHeaders = new Func<IDictionary, string, IEnumerable<string>>((maDict, key) =>
            {
                if (!maDict.Contains(key))
                    return [];

                return [(string)((dynamic)maDict[key]).StringValue];
            });

            transaction.AcceptDistributedTraceHeaders((IDictionary)messageAttributes, getHeaders, TransportType.Queue);

        }
    }
}
