// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Data;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Api;

namespace CompositeTests
{
    public static class AgentWrapperApiExtensions
    {
        public static ISegment StartTransactionSegmentOrThrow(this IAgent agent, string segmentName, MethodCall methodCall = null)
        {
            methodCall = methodCall ?? GetDefaultMethodCall(agent);
            var segment = agent.CurrentTransaction.StartTransactionSegment(methodCall, segmentName);
            if (segment == null)
                throw new NullReferenceException("segment");

            return segment;
        }

        public static ISegment StartCustomSegmentOrThrow(this IAgent agent, string segmentName, MethodCall methodCall = null)
        {
            methodCall = methodCall ?? GetCustomSegmentMethodCall(agent);
            var segment = agent.CurrentTransaction.StartCustomSegment(methodCall, segmentName);
            if (segment == null)
                throw new NullReferenceException("segment");

            return segment;
        }

        public static ISegment StartMethodSegmentOrThrow(this IAgent agent, string typeName, string methodName, MethodCall methodCall = null)
        {
            methodCall = methodCall ?? GetDefaultMethodCall(agent);
            var segment = agent.CurrentTransaction.StartMethodSegment(methodCall, typeName, methodName);
            if (segment == null)
                throw new NullReferenceException("segment");

            return segment;
        }

        public static ISegment StartExternalRequestSegmentOrThrow(this IAgent agent, Uri uri, string httpVerb, MethodCall methodCall = null)
        {
            methodCall = methodCall ?? GetDefaultMethodCall(agent);
            var segment = agent.CurrentTransaction.StartExternalRequestSegment(methodCall, uri, httpVerb);
            if (segment == null)
                throw new NullReferenceException("segment");

            return segment;
        }

        public static ISegment StartDatastoreRequestSegmentOrThrow(this IAgent agent, string operation, DatastoreVendor vendor, string model, string commandText = null, MethodCall methodCall = null, string host = null, string portPathOrId = null, string databaseName = null, IDictionary<string, IConvertible> queryParameters = null)
        {
            methodCall = methodCall ?? GetDefaultMethodCall(agent);
            var segment = agent.CurrentTransaction.StartDatastoreSegment(methodCall, new ParsedSqlStatement(vendor, model, operation), new ConnectionInfo(host, portPathOrId, databaseName), commandText, queryParameters);
            if (segment == null)
                throw new NullReferenceException("segment");

            return segment;
        }

        public static ISegment StartDatastoreRequestSegmentOrThrow(this IAgent agent, DatastoreVendor vendor, CommandType commandType, string commandText = null, MethodCall methodCall = null, string host = null, string portPathOrId = null, string databaseName = null, IDictionary<string, IConvertible> queryParameters = null)
        {
            methodCall = methodCall ?? GetDefaultMethodCall(agent);
            var parsedStatement = agent.CurrentTransaction.GetParsedDatabaseStatement(vendor, commandType, commandText);

            var segment = agent.CurrentTransaction.StartDatastoreSegment(methodCall, parsedStatement, new ConnectionInfo(host, portPathOrId, databaseName), commandText, queryParameters);
            if (segment == null)
                throw new NullReferenceException("segment");

            return segment;
        }

        public static ISegment StartMessageBrokerSegmentOrThrow(this IAgent agent, string vendor, MessageBrokerDestinationType destinationType, string destination, MessageBrokerAction action, MethodCall methodCall = null)
        {
            methodCall = methodCall ?? GetDefaultMethodCall(agent);
            var segment = agent.CurrentTransaction.StartMessageBrokerSegment(methodCall, destinationType, action, vendor, destination);
            if (segment == null)
                throw new NullReferenceException("segment");

            return segment;
        }

        private static MethodCall GetDefaultMethodCall(IAgent agent)
        {
            return new MethodCall(
                new Method(agent.GetType(), "methodName", "parameterTypeNames"),
                agent,
                new object[0]
                );
        }

        private static MethodCall GetCustomSegmentMethodCall(IAgent agent)
        {
            return new MethodCall(
                new Method(agent.GetType(), "methodName", "parameterTypeNames"),
                agent,
                new object[] { "customName" }
                );
        }
    }
}
