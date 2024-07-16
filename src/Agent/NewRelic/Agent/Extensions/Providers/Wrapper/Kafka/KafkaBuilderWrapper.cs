// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Kafka
{
    public class KafkaBuilderWrapper : IWrapper
    {
        private Func<object, IEnumerable> _producerBuilderConfigGetter;
        private Func<object, IEnumerable> _consumerBuilderConfigGetter;

        private const string WrapperName = "KafkaBuilderWrapper";
        private const string BootstrapServersKey = "bootstrap.servers";

        public bool IsTransactionRequired => false;
        public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(instrumentedMethodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var builder = instrumentedMethodCall.MethodCall.InvocationTarget;

            dynamic configuration = null;

            if (builder.GetType().Name == "ProducerBuilder`2")
            {
                var configGetter = _producerBuilderConfigGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<IEnumerable>(builder.GetType(), "Config");
                configuration = configGetter(builder) as dynamic;
            }
            else if (builder.GetType().Name == "ConsumerBuilder`2")
            {
                var configGetter = _consumerBuilderConfigGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<IEnumerable>(builder.GetType(), "Config");
                configuration = configGetter(builder) as dynamic;
            }

            if (configuration == null)
                return Delegates.NoOp;

            string bootstrapServers = null;

            foreach (var kvp in configuration)
            {
                if (kvp.Key == BootstrapServersKey)
                {
                    bootstrapServers = kvp.Value as string;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(bootstrapServers))
                return Delegates.GetDelegateFor<object>(onSuccess: (producerOrConsumerAsObject) =>
                {
                    KafkaHelper.AddBootstrapServersToCache(producerOrConsumerAsObject, bootstrapServers);
                });

            return Delegates.NoOp;

        }
    }
}
