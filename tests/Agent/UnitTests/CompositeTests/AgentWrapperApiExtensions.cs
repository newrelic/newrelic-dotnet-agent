﻿using System;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace CompositeTests
{
	public static class AgentWrapperApiExtensions
	{
		[NotNull]
		public static ISegment StartTransactionSegmentOrThrow([NotNull] this IAgentWrapperApi agentWrapperApi, [NotNull] String segmentName, MethodCall methodCall = null)
		{
			methodCall = methodCall ?? GetDefaultMethodCall(agentWrapperApi);
			var segment = agentWrapperApi.CurrentTransaction.StartTransactionSegment(methodCall, segmentName);
			if (segment == null)
				throw new NullReferenceException("segment");

			return segment;
		}

		[NotNull]
		public static ISegment StartCustomSegmentOrThrow([NotNull] this IAgentWrapperApi agentWrapperApi, [NotNull] String segmentName, MethodCall methodCall = null)
		{
			methodCall = methodCall ?? GetCustomSegmentMethodCall(agentWrapperApi);
			var segment = agentWrapperApi.CurrentTransaction.StartCustomSegment(methodCall, segmentName);
			if (segment == null)
				throw new NullReferenceException("segment");

			return segment;
		}

		[NotNull]
		public static ISegment StartMethodSegmentOrThrow([NotNull] this IAgentWrapperApi agentWrapperApi, [NotNull] String typeName, [NotNull] String methodName, MethodCall methodCall = null)
		{
			methodCall = methodCall ?? GetDefaultMethodCall(agentWrapperApi);
			var segment = agentWrapperApi.CurrentTransaction.StartMethodSegment(methodCall, typeName, methodName);
			if (segment == null)
				throw new NullReferenceException("segment");

			return segment;
		}

		[NotNull]
		public static ISegment StartExternalRequestSegmentOrThrow([NotNull] this IAgentWrapperApi agentWrapperApi, [NotNull] Uri uri, [NotNull] String httpVerb, MethodCall methodCall = null)
		{
			methodCall = methodCall ?? GetDefaultMethodCall(agentWrapperApi);
			var segment = agentWrapperApi.CurrentTransaction.StartExternalRequestSegment(methodCall, uri, httpVerb);
			if (segment == null)
				throw new NullReferenceException("segment");

			return segment;
		}

		[NotNull]
		public static ISegment StartDatastoreRequestSegmentOrThrow([NotNull] this IAgentWrapperApi agentWrapperApi, String operation, DatastoreVendor vendor, String model, String commandText = null, MethodCall methodCall = null, String host = null, String portPathOrId = null, String databaseName = null)
		{
			methodCall = methodCall ?? GetDefaultMethodCall(agentWrapperApi);
			var segment = agentWrapperApi.CurrentTransaction.StartDatastoreSegment(methodCall, operation, vendor, model, commandText, host, portPathOrId, databaseName);
			if (segment == null)
				throw new NullReferenceException("segment");

			return segment;
		}

		[NotNull]
		public static ISegment StartMessageBrokerSegmentOrThrow([NotNull] this IAgentWrapperApi agentWrapperApi, [NotNull] String vendor, MessageBrokerDestinationType destinationType, String destination, MessageBrokerAction action, MethodCall methodCall = null)
		{
			methodCall = methodCall ?? GetDefaultMethodCall(agentWrapperApi);
			var segment = agentWrapperApi.CurrentTransaction.StartMessageBrokerSegment(methodCall, destinationType, action, vendor, destination);
			if (segment == null)
				throw new NullReferenceException("segment");

			return segment;
		}

		private static MethodCall GetDefaultMethodCall([NotNull] IAgentWrapperApi agentWrapperApi)
		{
			return new MethodCall(
				new Method(agentWrapperApi.GetType(), "methodName", "parameterTypeNames"),
				agentWrapperApi,
				new Object[0]
				);
		}

		private static MethodCall GetCustomSegmentMethodCall([NotNull] IAgentWrapperApi agentWrapperApi)
		{
			return new MethodCall(
				new Method(agentWrapperApi.GetType(), "methodName", "parameterTypeNames"),
				agentWrapperApi,
				new Object[1] { "customName" }
				);
		}
		}
}
