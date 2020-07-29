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
            var canWrap = method.MatchesAny(assemblyName: "RabbitMQ.Client", typeName: "RabbitMQ.Client.Events.EventingBasicConsumer", methodName: "HandleBasicDeliver");
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            // (IBasicConsumer) void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
            var routingKey = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(4);
            var destType = RabbitMqHelper.GetBrokerDestinationType(routingKey);
            var destName = RabbitMqHelper.ResolveDestinationName(destType, routingKey);

            transaction = agentWrapperApi.CreateMessageBrokerTransaction(destType, RabbitMqHelper.VendorName, routingKey);

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, destType, MessageBrokerAction.Consume, RabbitMqHelper.VendorName, destName);

            return Delegates.GetDelegateFor(
                onFailure: transaction.NoticeError,
                onComplete: () =>
                {
                    segment.End();
                    transaction.End();
                });
        }
    }
}
