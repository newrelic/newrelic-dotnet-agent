// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
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

        private static Func<object, object> _modelGetter;
        private static Func<object, object> _sessionGetter;
        private static Func<object, object> _framingConnectionGetter;
        private static Func<object, object> _autorecoveringConnectionGetter5OrOlder;
        private static Func<object, object> _autorecoveringConnectionGetter6OrNewer;
        private static Func<object, object> _endpointGetter;
        private static Func<object, string> _hostnameGetter;
        private static Func<object, int> _portGetter;

        private ConcurrentDictionary<Type, Func<Type, object, object>> _connectionGetter =
                    new ConcurrentDictionary<Type, Func<Type, object, object>>();

        private static bool _hasGetServerFailed = false;

        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            // (IBasicConsumer) void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
            // (V7 IAsyncBasicConsumer) Task HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
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

            GetServerDetails(instrumentedMethodCall, out var hostname, out var port, agent);

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

        private void GetServerDetails(InstrumentedMethodCall instrumentedMethodCall, out string hostname, out int? port, IAgent agent)
        {
            if (_hasGetServerFailed)
            {
                hostname = null;
                port = null;
            }

            try
            {
                // v7 renamed "model" to "channel"
                if (RabbitMqHelper.GetRabbitMQVersion(instrumentedMethodCall.MethodCall.InvocationTarget.GetType()) >= 7)
                {
                    _modelGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(instrumentedMethodCall.MethodCall.InvocationTarget.GetType(), "Channel");
                }
                else
                {
                    _modelGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(instrumentedMethodCall.MethodCall.InvocationTarget.GetType(), "Model");
                }
                var model = _modelGetter(instrumentedMethodCall.MethodCall.InvocationTarget);

                object connection = null;
                var modelType = model.GetType();
                var connectionGetter = _connectionGetter.GetOrAdd(modelType, GetConnectionForType);
                if (connectionGetter != null)
                {
                    connection = connectionGetter(modelType, model);
                }

                // catches both connectionGetter == null and connection == null
                if (connection == null)
                {
                    hostname = null;
                    port = null;
                    return;
                }

                _endpointGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(connection.GetType(), "Endpoint");
                var endpoint = _endpointGetter(connection);

                _hostnameGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(endpoint.GetType(), "HostName");
                hostname = _hostnameGetter(endpoint);

                _portGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<int>(endpoint.GetType(), "Port");
                port = _portGetter(endpoint);

                static Func<Type, object, object> GetConnectionForType(Type modelType)
                {
                    var version = RabbitMqHelper.GetRabbitMQVersion(modelType); // caches version in RabbitMqHelper.
                    return modelType.ToString() switch
                    {
                        "RabbitMQ.Client.Framing.Impl.Model" => GetConnectionFromFramingModel,
                        "RabbitMQ.Client.Impl.AutorecoveringModel" when version <= 5 =>
                            GetConnectionFromAutorecoveryModel5OrOlder,
                        "RabbitMQ.Client.Impl.AutorecoveringModel" when version == 6 =>
                            GetConnectionFromAutorecoveryModel6OrNewer,
                        "RabbitMQ.Client.Impl.AutorecoveringChannel" when version >= 7 =>
                            GetConnectionFromAutorecoveryModel6OrNewer,
                        _ => null
                    };
                }

                static object GetConnectionFromFramingModel(Type modelType, object model)
                {
                    // <= v4
                    _sessionGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(modelType, "Session");
                    var session = _sessionGetter(model);

                    _framingConnectionGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(session.GetType(), "Connection");
                    return _framingConnectionGetter(session);
                }

                static object GetConnectionFromAutorecoveryModel5OrOlder(Type modelType, object model)
                {
                    _autorecoveringConnectionGetter5OrOlder = VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>(modelType, "m_connection");
                    return _autorecoveringConnectionGetter5OrOlder(model);
                }

                static object GetConnectionFromAutorecoveryModel6OrNewer(Type modelType, object model)
                {
                    _autorecoveringConnectionGetter6OrNewer = VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>(modelType, "_connection");
                    return _autorecoveringConnectionGetter6OrNewer(model);
                }
            }
            catch (Exception exception)
            {
                agent.Logger.Warn(exception, "Unable to get RabbitMQ server address/port due to differences in the expected types. Server address/port attributes will not be available.");
                _hasGetServerFailed = true;
                hostname = null;
                port = null;
            }
        }
    }
}
