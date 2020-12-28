// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
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
                    new MethodSignature("_Private_BasicPublish","System.String,System.String,System.Boolean,RabbitMQ.Client.IBasicProperties,System.ReadOnlyMemory`1[System.Byte]"), // 6.2.1
				});
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            int rabbitVersion = GetRabbitMQVersion(instrumentedMethodCall);
            // 3.6.0+ (5.1.0+) (IModel)void BasicPublish(string exchange, string routingKey, bool mandatory, IBasicProperties basicProperties, byte[] body)

            var segment = (rabbitVersion >= 6) ?
                RabbitMqHelper.CreateSegmentForPublishWrappers6Plus(instrumentedMethodCall, transaction, agent.Configuration, BasicPropertiesIndex) :
                RabbitMqHelper.CreateSegmentForPublishWrappers(instrumentedMethodCall, transaction, agent.Configuration, BasicPropertiesIndex);

            return Delegates.GetDelegateFor(segment);
        }


        private int GetRabbitMQVersion(InstrumentedMethodCall methodCall)
        {
            var fullName = methodCall.MethodCall.Method.Type.Assembly.ManifestModule.Assembly.FullName;
            var versionString = "Version=";
            var length = versionString.Length;
            return Int32.Parse(fullName.Substring(fullName.IndexOf(versionString) + length, 1));
        }
    }
}
