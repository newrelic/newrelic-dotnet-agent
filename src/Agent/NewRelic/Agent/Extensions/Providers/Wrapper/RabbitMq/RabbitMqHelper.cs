// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.RabbitMq
{
    public class RabbitMqHelper
    {
        private const string TempQueuePrefix = "amq.";
        private const string BasicPropertiesType = "RabbitMQ.Client.Framing.BasicProperties";
        public const string VendorName = "RabbitMQ";
        public const string AssemblyName = "RabbitMQ.Client";
        public const string TypeName = "RabbitMQ.Client.Framing.Impl.Model";

        private static Func<object, object> _getHeadersFunc;
        public static Dictionary<string, object> GetHeaders(object Properties)
        {
            var func = _getHeadersFunc ?? (_getHeadersFunc = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(Properties.GetType(), "Headers"));
            return func(Properties) as Dictionary<string, object>;
        }

        private static Action<Dictionary<string, object>> _setHeadersAction;
        public static void SetHeaders(object Properties, Dictionary<string, object> headers)
        {
            var action = _setHeadersAction ??
                (_setHeadersAction = VisibilityBypasser.Instance.GeneratePropertySetter<Dictionary<string, object>>(Properties, "Headers"));

            action(headers);
        }

        public static MessageBrokerDestinationType GetBrokerDestinationType(string queueNameOrRoutingKey)
        {
            if (queueNameOrRoutingKey.StartsWith(TempQueuePrefix))
                return MessageBrokerDestinationType.TempQueue;

            return queueNameOrRoutingKey.Contains(".") ? MessageBrokerDestinationType.Topic : MessageBrokerDestinationType.Queue;
        }

        public static string ResolveDestinationName(MessageBrokerDestinationType destinationType,
            string queueNameOrRoutingKey)
        {
            return (destinationType == MessageBrokerDestinationType.TempQueue ||
                    destinationType == MessageBrokerDestinationType.TempTopic)
                ? null
                : queueNameOrRoutingKey;
        }

        public static bool TryGetPayloadFromHeaders(Dictionary<string, object> messageHeaders, IAgent agent,
            out string serializedPayload)
        {
            if (agent.TryGetDistributedTracePayloadFromHeaders(messageHeaders, out var payload))
            {
                serializedPayload = Encoding.UTF8.GetString((byte[])(payload));
                return true;
            }

            serializedPayload = null;
            return false;
        }

        public static ISegment CreateSegmentForPublishWrappers(InstrumentedMethodCall instrumentedMethodCall, ITransaction transaction, int basicPropertiesIndex)
        {
            // ATTENTION: We have validated that the use of dynamic here is appropriate based on the visibility of the data we're working with.
            // If we implement newer versions of the API or new methods we'll need to re-evaluate.
            // never null. Headers property can be null.
            var basicProperties = instrumentedMethodCall.MethodCall.MethodArguments.ExtractAs<dynamic>(basicPropertiesIndex);

            var routingKey = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(1);
            var destType = GetBrokerDestinationType(routingKey);
            var destName = ResolveDestinationName(destType, routingKey);

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, destType, MessageBrokerAction.Produce, VendorName, destName);

            //If the RabbitMQ version doesn't provide the BasicProperties parameter we just bail.
            if (basicProperties.GetType().FullName != BasicPropertiesType)
            {
                return segment;
            }

            var setHeaders = new Action<dynamic, string, string>((carrier, key, value) =>
            {
                Dictionary<string, object> headers = carrier.Headers as Dictionary<string, object>;
                if (headers == null)
                {
                    headers = new Dictionary<string, object>();
                    carrier.Headers = headers;
                }

                headers[key] = value;
            });

            transaction.InsertDistributedTraceHeaders(basicProperties, setHeaders);

            return segment;
        }

        public static ISegment CreateSegmentForPublishWrappers6Plus(InstrumentedMethodCall instrumentedMethodCall, ITransaction transaction, int basicPropertiesIndex)
        {
            var basicProperties = instrumentedMethodCall.MethodCall.MethodArguments.ExtractAs<object>(basicPropertiesIndex);

            var routingKey = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(1);
            var destType = GetBrokerDestinationType(routingKey);
            var destName = ResolveDestinationName(destType, routingKey);

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, destType, MessageBrokerAction.Produce, VendorName, destName);

            //If the RabbitMQ version doesn't provide the BasicProperties parameter we just bail.
            if (basicProperties.GetType().FullName != BasicPropertiesType)
            {
                
                return segment;
            }

            var setHeaders = new Action<object, string, string>((carrier, key, value) =>
            {
                var headers = GetHeaders(carrier);
                if (headers == null)
                {
                    headers = new Dictionary<string, object>();
                    SetHeaders(carrier, headers);
                }

                headers[key] = value;
            });

            transaction.InsertDistributedTraceHeaders(basicProperties, setHeaders);

            return segment;
        }
    }
}
