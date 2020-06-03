using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.RabbitMq
{
    public class BasicPublishWrapper : IWrapper
    {
        private const int BasicPropertiesIndex = 3;

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: RabbitMqHelper.AssemblyName, typeName: RabbitMqHelper.TypeName,
                methodSignatures: new[]
                {
                    new MethodSignature("_Private_BasicPublish","System.String,System.String,System.Boolean,RabbitMQ.Client.IBasicProperties,System.Byte[]"), // 3.6.0+ (5.1.0+)
				});
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            // 3.6.0+ (5.1.0+) (IModel)void BasicPublish(string exchange, string routingKey, bool mandatory, IBasicProperties basicProperties, byte[] body)
            var segment = RabbitMqHelper.CreateSegmentForPublishWrappers(instrumentedMethodCall, transaction, agent.Configuration, BasicPropertiesIndex);

            return Delegates.GetDelegateFor(segment);
        }
    }
}
