using System;
using System.Collections.Generic;
using System.Data;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Parsing;

namespace CompositeTests
{
	public static class AgentWrapperApiExtensions
	{
		[NotNull]
		public static ISegment StartTransactionSegmentOrThrow([NotNull] this IAgent agent, [NotNull] String segmentName, MethodCall methodCall = null)
		{
			methodCall = methodCall ?? GetDefaultMethodCall(agent);
			var segment = agent.CurrentTransaction.StartTransactionSegment(methodCall, segmentName);
			if (segment == null)
				throw new NullReferenceException("segment");

			return segment;
		}

		[NotNull]
		public static ISegment StartCustomSegmentOrThrow([NotNull] this IAgent agent, [NotNull] String segmentName, MethodCall methodCall = null)
		{
			methodCall = methodCall ?? GetCustomSegmentMethodCall(agent);
			var segment = agent.CurrentTransaction.StartCustomSegment(methodCall, segmentName);
			if (segment == null)
				throw new NullReferenceException("segment");

			return segment;
		}

		[NotNull]
		public static ISegment StartMethodSegmentOrThrow([NotNull] this IAgent agent, [NotNull] String typeName, [NotNull] String methodName, MethodCall methodCall = null)
		{
			methodCall = methodCall ?? GetDefaultMethodCall(agent);
			var segment = agent.CurrentTransaction.StartMethodSegment(methodCall, typeName, methodName);
			if (segment == null)
				throw new NullReferenceException("segment");

			return segment;
		}

		[NotNull]
		public static ISegment StartExternalRequestSegmentOrThrow([NotNull] this IAgent agent, [NotNull] Uri uri, [NotNull] String httpVerb, MethodCall methodCall = null)
		{
			methodCall = methodCall ?? GetDefaultMethodCall(agent);
			var segment = agent.CurrentTransaction.StartExternalRequestSegment(methodCall, uri, httpVerb);
			if (segment == null)
				throw new NullReferenceException("segment");

			return segment;
		}

		[NotNull]
		public static ISegment StartDatastoreRequestSegmentOrThrow([NotNull] this IAgent agent, String operation, DatastoreVendor vendor, String model, String commandText = null, MethodCall methodCall = null, String host = null, String portPathOrId = null, String databaseName = null, IDictionary<string,IConvertible> queryParameters = null)
		{
			methodCall = methodCall ?? GetDefaultMethodCall(agent);
			var segment = agent.CurrentTransaction.StartDatastoreSegment(methodCall, new ParsedSqlStatement(vendor, model, operation), new ConnectionInfo(host, portPathOrId, databaseName), commandText, queryParameters);
			if (segment == null)
				throw new NullReferenceException("segment");

			return segment;
		}

		public static ISegment StartDatastoreRequestSegmentOrThrow( this IAgent agent, DatastoreVendor vendor, CommandType commandType, String commandText = null, MethodCall methodCall = null, String host = null, String portPathOrId = null, String databaseName = null, IDictionary<string, IConvertible> queryParameters = null)
		{
			methodCall = methodCall ?? GetDefaultMethodCall(agent);
			var parsedStatement = agent.CurrentTransaction.GetParsedDatabaseStatement(vendor, commandType, commandText);

			var segment = agent.CurrentTransaction.StartDatastoreSegment(methodCall, parsedStatement, new ConnectionInfo(host, portPathOrId, databaseName), commandText, queryParameters);
			if (segment == null)
				throw new NullReferenceException("segment");

			return segment;
		}

		[NotNull]
		public static ISegment StartMessageBrokerSegmentOrThrow([NotNull] this IAgent agent, [NotNull] String vendor, MessageBrokerDestinationType destinationType, String destination, MessageBrokerAction action, MethodCall methodCall = null)
		{
			methodCall = methodCall ?? GetDefaultMethodCall(agent);
			var segment = agent.CurrentTransaction.StartMessageBrokerSegment(methodCall, destinationType, action, vendor, destination);
			if (segment == null)
				throw new NullReferenceException("segment");

			return segment;
		}

		private static MethodCall GetDefaultMethodCall([NotNull] IAgent agent)
		{
			return new MethodCall(
				new Method(agent.GetType(), "methodName", "parameterTypeNames"),
				agent,
				new Object[0]
				);
		}

		private static MethodCall GetCustomSegmentMethodCall([NotNull] IAgent agent)
		{
			return new MethodCall(
				new Method(agent.GetType(), "methodName", "parameterTypeNames"),
				agent,
				new Object[] { "customName" }
				);
		}
	}
}
