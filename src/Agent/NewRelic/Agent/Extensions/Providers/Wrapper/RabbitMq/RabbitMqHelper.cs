// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using NewRelic.Agent.Extensions.SystemExtensions;

namespace NewRelic.Providers.Wrapper.RabbitMq
{
    public class RabbitMqHelper
    {
        private const string TempQueuePrefix = "amq.";
        private const string BasicPropertiesType = "RabbitMQ.Client.Framing.BasicProperties";
        private const string BasicProperties7PlusType = "RabbitMQ.Client.BasicProperties";
        public const string VendorName = "RabbitMQ";
        public const string AssemblyName = "RabbitMQ.Client";
        public const string TypeName = "RabbitMQ.Client.Framing.Impl.Model";

        private static Func<object, object> _sessionGetter;
        private static Func<object, object> _connectionGetter;
        private static Func<object, object> _endpointGetter;
        private static Func<object, string> _hostnameGetter;
        private static Func<object, int> _portGetter;

        private static Func<object, object> _getHeadersFunc;

        private static int? _version;
        private static bool _hasGetServerFailed = false;

        public static IDictionary<string, object> GetHeaders(object properties)
        {
            var func = _getHeadersFunc ?? (_getHeadersFunc = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(properties.GetType(), "Headers"));
            return func(properties) as IDictionary<string, object>;
        }
#nullable enable
        private static Func<object, object>? _getHeaders7PlusFunc = null;
        public static IDictionary<string, object?>? GetHeaders7Plus(object properties)
        {
            // v7: public IDictionary<string, object?>? Headers { get; set; }
            var func = _getHeaders7PlusFunc ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(properties.GetType(), "Headers");
            return func(properties) as IDictionary<string, object?>;
        }
        public static void SetHeaders7Plus(object properties, IDictionary<string, object?> headers)
        {
            // Unlike the GetHeaders function, we can't cache this action.  It is only valid for the specific Properties object instance provided.
            var action = VisibilityBypasser.Instance.GeneratePropertySetter<IDictionary<string, object?>>(properties, "Headers");

            action(headers);
        }
#nullable disable

        public static void SetHeaders(object properties, IDictionary<string, object> headers)
        {
            // Unlike the GetHeaders function, we can't cache this action.  It is only valid for the specific Properties object instance provided.
            var action = VisibilityBypasser.Instance.GeneratePropertySetter<IDictionary<string, object>>(properties, "Headers");

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

        public static ISegment CreateSegmentForPublishWrappers(InstrumentedMethodCall instrumentedMethodCall, ITransaction transaction, IAgent agent)
        {
            // ATTENTION: We have validated that the use of dynamic here is appropriate based on the visibility of the data we're working with.
            // If we implement newer versions of the API or new methods we'll need to re-evaluate.

            var routingKey = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(1);
            var destType = GetBrokerDestinationType(routingKey);
            var destName = ResolveDestinationName(destType, routingKey);

            var segment = transaction.StartMessageBrokerSegment(
                instrumentedMethodCall.MethodCall,
                destType,
                MessageBrokerAction.Produce,
                VendorName,
                destName,
                serverAddress: GetServerAddress(instrumentedMethodCall, agent),
                serverPort: GetServerPort(instrumentedMethodCall, agent),
                routingKey: routingKey);

            return segment;
        }
        public static void InsertDTHeaders(InstrumentedMethodCall instrumentedMethodCall, ITransaction transaction, int basicPropertiesIndex)
        {
            var basicProperties = instrumentedMethodCall.MethodCall.MethodArguments.ExtractAs<dynamic>(basicPropertiesIndex);
            //If the RabbitMQ version doesn't provide the BasicProperties parameter we just bail.
            if (basicProperties.GetType().FullName != BasicPropertiesType)
            {
                return;
            }

            var setHeaders = new Action<dynamic, string, string>((carrier, key, value) =>
            {
                var headers = carrier.Headers as IDictionary<string, object>;

                if (headers == null)
                {
                    headers = new Dictionary<string, object>();
                    carrier.Headers = headers;
                }
                else if (headers is IReadOnlyDictionary<string, object>)
                {
                    headers = new Dictionary<string, object>(headers);
                    carrier.Headers = headers;
                }

                headers[key] = value;
            });

            transaction.InsertDistributedTraceHeaders(basicProperties, setHeaders);

        }

        public static ISegment CreateSegmentForPublishWrappers6Plus(InstrumentedMethodCall instrumentedMethodCall, ITransaction transaction, IAgent agent)
        {
            var routingKey = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(1);
            var destType = GetBrokerDestinationType(routingKey);
            var destName = ResolveDestinationName(destType, routingKey);

            var segment = transaction.StartMessageBrokerSegment(
                instrumentedMethodCall.MethodCall,
                destType,
                MessageBrokerAction.Produce,
                VendorName,
                destName,
                serverAddress: GetServerAddress(instrumentedMethodCall, agent),
                serverPort: GetServerPort(instrumentedMethodCall, agent),
                routingKey: routingKey);

            return segment;
        }

        public static void InsertDTHeaders6Plus(InstrumentedMethodCall instrumentedMethodCall, ITransaction transaction, int basicPropertiesIndex)
        {
            // v7+ basicProperties type is IReadOnlyBasicProperties
            var basicProperties = instrumentedMethodCall.MethodCall.MethodArguments.ExtractAs<object>(basicPropertiesIndex);
            if (GetRabbitMQVersion(instrumentedMethodCall) >= 7)
            {
                // in v7, the properties property can sometimes be `EmptyBasicProperty` which
                // can't be modified. 
                // So for now, if the property isn't a `BasicProperties` type, we just bail
                if (basicProperties.GetType().FullName != BasicProperties7PlusType)
                {
                    return;
                }
#nullable enable
                var setHeaders = new Action<object, string, string>((carrier, key, value) =>
                {
                    var headers = GetHeaders7Plus(carrier);

                    if (headers == null)
                    {
                        headers = new Dictionary<string, object?>();
                        SetHeaders7Plus(carrier, headers);
                    }
                    else if (headers is IReadOnlyDictionary<string, object?>)
                    {
                        headers = new Dictionary<string, object?>(headers);
                        SetHeaders7Plus(carrier, headers);
                    }

                    headers[key] = value;
                });
                transaction.InsertDistributedTraceHeaders(basicProperties, setHeaders);
#nullable disable
            }
            else  // v6
            {
                //If the RabbitMQ version doesn't provide the BasicProperties parameter we just bail.
                if (basicProperties.GetType().FullName != BasicPropertiesType)
                {

                    return;
                }

                var setHeaders = new Action<object, string, string>((carrier, key, value) =>
                {
                    var headers = GetHeaders(carrier);

                    if (headers == null)
                    {
                        headers = new Dictionary<string, object>();
                        SetHeaders(carrier, headers);
                    }
                    else if (headers is IReadOnlyDictionary<string, object>)
                    {
                        headers = new Dictionary<string, object>(headers);
                        SetHeaders(carrier, headers);
                    }

                    headers[key] = value;
                });

                transaction.InsertDistributedTraceHeaders(basicProperties, setHeaders);
            }
        }

        public static int GetRabbitMQVersion(InstrumentedMethodCall methodCall)
        {
            if (_version.HasValue)
            {
                return _version.Value;
            }

            var fullName = methodCall.MethodCall.Method.Type.Assembly.ManifestModule.Assembly.FullName;
            var versionString = "Version=";
            _version = Int32.Parse(fullName.Substring(fullName.IndexOf(versionString) + versionString.Length, 1));
            return _version.Value;
        }

        public static int GetRabbitMQVersion(Type type)
        {
            if (_version.HasValue)
            {
                return _version.Value;
            }

            var fullName = type.Assembly.ManifestModule.Assembly.FullName;
            var versionString = "Version=";
            _version = Int32.Parse(fullName.Substring(fullName.IndexOf(versionString) + versionString.Length, 1));
            return _version.Value;
        }

        public static string GetServerAddress(InstrumentedMethodCall instrumentedMethodCall, IAgent agent)
        {
            if (_hasGetServerFailed)
            {
                return null;
            }

            try
            {
                _sessionGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(instrumentedMethodCall.MethodCall.InvocationTarget.GetType(), "Session");
                var session = _sessionGetter(instrumentedMethodCall.MethodCall.InvocationTarget);

                _connectionGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(session.GetType(), "Connection");
                var connection = _connectionGetter(session);

                _endpointGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(connection.GetType(), "Endpoint");
                var endpoint = _endpointGetter(connection);

                _hostnameGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(endpoint.GetType(), "HostName");
                return _hostnameGetter(endpoint);
            }
            catch (Exception exception)
            {
                agent.Logger.Warn(exception, "Unable to get RabbitMQ server address/port due to differences in the expected types. Server address/port attributes will not be available.");
                _hasGetServerFailed = true;
                return null;
            }
        }

        public static int? GetServerPort(InstrumentedMethodCall instrumentedMethodCall, IAgent agent)
        {
            if (_hasGetServerFailed)
            {
                return null;
            }

            try
            {
                _sessionGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(instrumentedMethodCall.MethodCall.InvocationTarget.GetType(), "Session");
                var session = _sessionGetter(instrumentedMethodCall.MethodCall.InvocationTarget);

                _connectionGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(session.GetType(), "Connection");
                var connection = _connectionGetter(session);

                _endpointGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(connection.GetType(), "Endpoint");
                var endpoint = _endpointGetter(connection);

                _portGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<int>(endpoint.GetType(), "Port");
                return _portGetter(endpoint);
            }
            catch (Exception exception)
            {
                agent.Logger.Warn(exception, "Unable to get RabbitMQ server address/port due to differences in the expected types. Server address/port attributes will not be available.");
                _hasGetServerFailed = true;
                return null;
            }
        }
    }
}
