/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using System.Text;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.RabbitMq
{
    public class HandleBasicDeliverWrapper : IWrapper
    {
        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: RabbitMqHelper.AssemblyName, typeName: "RabbitMQ.Client.Events.EventingBasicConsumer", methodName: "HandleBasicDeliver");
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            // (IBasicConsumer) void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
            var routingKey = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(4);
            var destType = RabbitMqHelper.GetBrokerDestinationType(routingKey);
            var destName = RabbitMqHelper.ResolveDestinationName(destType, routingKey);

            transaction = agent.CreateTransaction(
                destinationType: destType,
                brokerVendorName: RabbitMqHelper.VendorName,
                destination: routingKey);

            // ATTENTION: We have validated that the use of dynamic here is appropriate based on the visibility of the data we're working with.
            // If we implement newer versions of the API or new methods we'll need to re-evaluate.
            // basicProperties is never null (framework supplies it), though the Headers property could be
            var basicProperties = instrumentedMethodCall.MethodCall.MethodArguments.ExtractAs<dynamic>(5);
            var headers = (Dictionary<string, object>)basicProperties.Headers;

            agent.CurrentTransaction.AcceptDistributedTraceHeaders(headers, GetHeaderValue, TransportType.AMQP);

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, destType, MessageBrokerAction.Consume, RabbitMqHelper.VendorName, destName);

            return Delegates.GetDelegateFor(
                onFailure: transaction.NoticeError,
                onComplete: () =>
                {
                    segment.End();
                    transaction.End();
                });

            IEnumerable<string> GetHeaderValue(Dictionary<string, object> carrier, string key)
            {
                if (headers != null)
                {
                    var headerValues = new List<string>();
                    foreach (var item in headers)
                    {
                        if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                        {
                            headerValues.Add(Encoding.UTF8.GetString((byte[])headers[key]));
                        }
                    }

                    return headerValues;
                }

                return null;
            }
        }
    }
}
