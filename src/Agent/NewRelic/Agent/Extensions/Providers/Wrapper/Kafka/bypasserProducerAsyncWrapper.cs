// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.Kafka
{
    public class bypasserProducerAsyncWrapper : IWrapper
    {
        private static Func<object, object> _getHeadersFunc;
        private static Func<string, byte[], object> _getHeaderCtorFunc;
        private static Func<object, object, object> _setHeaderFunc;

        private const string WrapperName = "KafkaProducerAsyncx";
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var topic = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(0);
            var message = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<object>(1);

            var segment = transaction.StartMessageBrokerSegment(instrumentedMethodCall.MethodCall, MessageBrokerDestinationType.Topic, MessageBrokerAction.Produce, "Confluent", topic);

            var hctor = _getHeaderCtorFunc ??= VisibilityBypasser.Instance.GenerateTypeFactory<string, byte[]>("Confluent.Kafka", "Confluent.Kafka.Header");
            var addHeader = _setHeaderFunc ??= VisibilityBypasser.Instance.GenerateOneParameterOverloadedMethodCaller<object>("Confluent.Kafka", "Confluent.Kafka.Headers", "Add", "Confluent.Kafka.Header");

            var setHeaders = new Action<object, string, string>((carrier, key, value) =>
            {
                var newHeader = hctor(key, Encoding.ASCII.GetBytes(value));
                addHeader(carrier, newHeader);
            });

            var getHeaders = _getHeadersFunc ?? (_getHeadersFunc = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(message.GetType(), "Headers"));
            var headers = getHeaders(message);
            transaction.InsertDistributedTraceHeaders(headers, setHeaders);

            return Delegates.GetDelegateFor(segment);
        }

    }
}
