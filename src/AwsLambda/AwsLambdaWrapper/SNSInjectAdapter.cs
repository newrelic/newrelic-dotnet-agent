/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using Amazon.SimpleNotificationService.Model;
using OpenTracing;
using OpenTracing.Propagation;
using System;
using System.Collections;
using System.Collections.Generic;

namespace NewRelic.OpenTracing.AmazonLambda
{
    internal class SNSInjectAdapter : ITextMap
    {
        private readonly Dictionary<string, MessageAttributeValue> _messageAttributes;

        public SNSInjectAdapter(Dictionary<string, MessageAttributeValue> messageAttributes)
        {
            _messageAttributes = messageAttributes;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            throw new NotSupportedException(
                $"{nameof(SNSInjectAdapter)} should only be used with {nameof(ITracer)}.{nameof(ITracer.Inject)}");
        }

        public void Set(string key, string value)
        {
            if (!_messageAttributes.ContainsKey(key))
            {
                var messageAttributeValue = new MessageAttributeValue()
                {
                    DataType = "String",
                    StringValue = value
                };

                _messageAttributes.Add(key, messageAttributeValue);
            }
            else
            {
                Logger.Log(message: "New Relic key already exists in MessageAttributes collection.", rawLogging: false, level: "DEBUG");
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
