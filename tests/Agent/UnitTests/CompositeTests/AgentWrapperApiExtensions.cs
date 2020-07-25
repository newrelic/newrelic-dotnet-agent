using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace CompositeTests
{
    public static class AgentWrapperApiExtensions
    {
        public static ISegment StartTransactionSegmentOrThrow(this IAgentWrapperApi agentWrapperApi, String segmentName, MethodCall methodCall = null)
        {
            methodCall = methodCall ?? GetDefaultMethodCall(agentWrapperApi);
            var segment = agentWrapperApi.CurrentTransaction.StartTransactionSegment(methodCall, segmentName);
            if (segment == null)
                throw new NullReferenceException("segment");

            return segment;
        }
        public static ISegment StartCustomSegmentOrThrow(this IAgentWrapperApi agentWrapperApi, String segmentName, MethodCall methodCall = null)
        {
            methodCall = methodCall ?? GetCustomSegmentMethodCall(agentWrapperApi);
            var segment = agentWrapperApi.CurrentTransaction.StartCustomSegment(methodCall, segmentName);
            if (segment == null)
                throw new NullReferenceException("segment");

            return segment;
        }
        public static ISegment StartMethodSegmentOrThrow(this IAgentWrapperApi agentWrapperApi, String typeName, String methodName, MethodCall methodCall = null)
        {
            methodCall = methodCall ?? GetDefaultMethodCall(agentWrapperApi);
            var segment = agentWrapperApi.CurrentTransaction.StartMethodSegment(methodCall, typeName, methodName);
            if (segment == null)
                throw new NullReferenceException("segment");

            return segment;
        }
        public static ISegment StartExternalRequestSegmentOrThrow(this IAgentWrapperApi agentWrapperApi, Uri uri, String httpVerb, MethodCall methodCall = null)
        {
            methodCall = methodCall ?? GetDefaultMethodCall(agentWrapperApi);
            var segment = agentWrapperApi.CurrentTransaction.StartExternalRequestSegment(methodCall, uri, httpVerb);
            if (segment == null)
                throw new NullReferenceException("segment");

            return segment;
        }
        public static ISegment StartDatastoreRequestSegmentOrThrow(this IAgentWrapperApi agentWrapperApi, String operation, DatastoreVendor vendor, String model, String commandText = null, MethodCall methodCall = null, String host = null, String portPathOrId = null, String databaseName = null)
        {
            methodCall = methodCall ?? GetDefaultMethodCall(agentWrapperApi);
            var segment = agentWrapperApi.CurrentTransaction.StartDatastoreSegment(methodCall, operation, vendor, model, commandText, host, portPathOrId, databaseName);
            if (segment == null)
                throw new NullReferenceException("segment");

            return segment;
        }
        public static ISegment StartMessageBrokerSegmentOrThrow(this IAgentWrapperApi agentWrapperApi, String vendor, MessageBrokerDestinationType destinationType, String destination, MessageBrokerAction action, MethodCall methodCall = null)
        {
            methodCall = methodCall ?? GetDefaultMethodCall(agentWrapperApi);
            var segment = agentWrapperApi.CurrentTransaction.StartMessageBrokerSegment(methodCall, destinationType, action, vendor, destination);
            if (segment == null)
                throw new NullReferenceException("segment");

            return segment;
        }

        private static MethodCall GetDefaultMethodCall(IAgentWrapperApi agentWrapperApi)
        {
            return new MethodCall(
                new Method(agentWrapperApi.GetType(), "methodName", "parameterTypeNames"),
                agentWrapperApi,
                new Object[0]
                );
        }

        private static MethodCall GetCustomSegmentMethodCall(IAgentWrapperApi agentWrapperApi)
        {
            return new MethodCall(
                new Method(agentWrapperApi.GetType(), "methodName", "parameterTypeNames"),
                agentWrapperApi,
                new Object[1] { "customName" }
                );
        }
    }
}
