// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using MethodCall = NewRelic.Agent.Extensions.Providers.Wrapper.MethodCall;

namespace NewRelic.Providers.Wrapper.MassTransit
{
    public class NewRelicFilter : IFilter<ConsumeContext>, IFilter<PublishContext>, IFilter<SendContext>
    {
        private const string SendMethodName = "Send";
        private const string MessageBrokerVendorName = "MassTransit";

        private Method _consumeMethod;
        private Method _publishMethod;
        private Method _sendMethod;

        private Agent.Api.IAgent _agent;

        public NewRelicFilter(Agent.Api.IAgent agent)
        {
            _agent = agent;
        }

        public void Probe(ProbeContext context)
        {
            context.CreateFilterScope("newrelic-scope");
        }

        public async Task Send(ConsumeContext context, IPipe<ConsumeContext> next)
        {
            _ = _consumeMethod ??= new Method(context.GetType(), SendMethodName,
                context.GetType().FullName + "," + next.GetType().FullName);

            var mc = new MethodCall(_consumeMethod, context, default(string[]), true);

            var destName = MassTransitHelpers.GetQueue(context.SourceAddress);

            var transaction = _agent.CreateTransaction(
                destinationType: MassTransitHelpers.GetBrokerDestinationType(context.SourceAddress),
                brokerVendorName: MessageBrokerVendorName,
                destination: destName);

            transaction.AttachToAsync();
            transaction.DetachFromPrimary();

            transaction.AcceptDistributedTraceHeaders(context.Headers, GetHeaderValue, TransportType.AMQP);

            var segment = transaction.StartMessageBrokerSegment(mc, MessageBrokerDestinationType.Queue, MessageBrokerAction.Consume, MessageBrokerVendorName, destName);

            await next.Send(context);
            segment.End();
            transaction.End();

            IEnumerable<string> GetHeaderValue(Headers carrier, string key)
            {
                var headers = carrier.GetAll();
                if (headers != null)
                {
                    var headerValues = new List<string>();
                    foreach (var item in headers)
                    {
                        if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                        {
                            headerValues.Add(item.Value.ToString());
                        }
                    }

                    return headerValues;
                }

                return null;
            }
        }

        public async Task Send(PublishContext context, IPipe<PublishContext> next)
        {
            _ = _publishMethod ??= new Method(context.GetType(), SendMethodName,
                context.GetType().FullName + "," + next.GetType().FullName);

            var mc = new MethodCall(_publishMethod, context, default(string[]), true);

            var destName = MassTransitHelpers.GetQueue(context.SourceAddress);
            var destType = MassTransitHelpers.GetBrokerDestinationType(context.SourceAddress);

            var transaction = _agent.CurrentTransaction;
            MassTransitHelpers.InsertDistributedTraceHeaders(context.Headers, transaction);
            var segment = transaction.StartMessageBrokerSegment(mc, destType, MessageBrokerAction.Produce, MessageBrokerVendorName, destName);

            await next.Send(context);
            segment.End();
        }

        public async Task Send(SendContext context, IPipe<SendContext> next)
        {
            _ = _sendMethod ??= new Method(context.GetType(), SendMethodName,
                context.GetType().FullName + "," + next.GetType().FullName);

            var mc = new MethodCall(_sendMethod, context, default(string[]), true);

            var destName = MassTransitHelpers.GetQueue(context.SourceAddress);
            var destType = MassTransitHelpers.GetBrokerDestinationType(context.SourceAddress);

            var transaction = _agent.CurrentTransaction;
            MassTransitHelpers.InsertDistributedTraceHeaders(context.Headers, transaction);
            var segment = transaction.StartMessageBrokerSegment(mc, destType, MessageBrokerAction.Produce, MessageBrokerVendorName, destName);

            await next.Send(context);
            segment.End();
        }
    }
}
