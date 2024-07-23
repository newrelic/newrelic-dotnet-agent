// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.SystemExtensions;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.RabbitMq
{
    public class HandleBasicDeliverWrapper : IWrapper
    {
        private const string WrapperName = "HandleBasicDeliverWrapper";

        private Func<object, object> _modelGetter;
        private Func<object, object> _sessionGetter;
        private Func<object, object> _connectionGetter;
        private Func<object, object> _autorecoveringConnectionGetter;
        private Func<object, object> _endpointGetter;
        private Func<object, string> _hostnameGetter;
        private Func<object, int> _portGetter;

        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
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
            var basicProperties = instrumentedMethodCall.MethodCall.MethodArguments.ExtractAs<object>(5);
            var headers = RabbitMqHelper.GetHeaders(basicProperties);

            agent.CurrentTransaction.AcceptDistributedTraceHeaders(headers, GetHeaderValue, TransportType.AMQP);

            _modelGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(instrumentedMethodCall.MethodCall.InvocationTarget.GetType(), "Model");
            var model = _modelGetter(instrumentedMethodCall.MethodCall.InvocationTarget);

            object connection = null;
            if (model.GetType().ToString() == "RabbitMQ.Client.Framing.Impl.Model")
            {
                // <= v4
                _sessionGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(model.GetType(), "Session");
                var session = _sessionGetter(model);

                _connectionGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(session.GetType(), "Connection");
                connection = _connectionGetter(session);
            }
            else if (model.GetType().ToString() == "RabbitMQ.Client.Impl.AutorecoveringModel")
            {
                // 5.x is m_connection, 6.x is _connection,
                if (RabbitMqHelper.GetRabbitMQVersion(instrumentedMethodCall) <= 5)
                {
                    _autorecoveringConnectionGetter = VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>(model.GetType(), "m_connection");
                    connection = _autorecoveringConnectionGetter(model);
                }
                else if (RabbitMqHelper.GetRabbitMQVersion(instrumentedMethodCall) >= 6)
                {
                    _autorecoveringConnectionGetter = VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>(model.GetType(), "_connection");
                    connection = _autorecoveringConnectionGetter(model);
                }
            }
            
            _endpointGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(connection.GetType(), "Endpoint");
            var endpoint = _endpointGetter(connection);

            _hostnameGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(endpoint.GetType(), "HostName");
            var hostname = _hostnameGetter(endpoint);

            _portGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<int>(endpoint.GetType(), "Port");
            var port = _portGetter(endpoint);

            var segment = transaction.StartMessageBrokerSegment(
                instrumentedMethodCall.MethodCall,
                destType,
                MessageBrokerAction.Consume,
                RabbitMqHelper.VendorName,
                destName,
                serverAddress: hostname,
                serverPort: port,
                routingKey: routingKey);

            return Delegates.GetDelegateFor(
                onFailure: transaction.NoticeError,
                onComplete: () =>
                {
                    segment.End();
                    transaction.End();
                });

            IEnumerable<string> GetHeaderValue(IDictionary<string, object> carrier, string key)
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
