// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace NewRelic.Providers.Wrapper.Wcf3
{
    public class ChannelFactoryWrapper : IWrapper
    {
        private static readonly List<Type> _bindingsSent = new List<Type>();
        private static readonly object _bindingLock = new object();

        public bool IsTransactionRequired => false;

        private const string WrapperName = "ChannelFactoryWrapper";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(instrumentedMethodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            return Delegates.GetDelegateFor(onComplete: () =>
            {
                var channelFactory = instrumentedMethodCall.MethodCall.InvocationTarget as ChannelFactory;
                if(channelFactory == null)
                {
                    return;
                }

                var bindingType = channelFactory.Endpoint.Binding.GetType();

                channelFactory.Endpoint.Behaviors.Add(new NewRelicEndpointBehavior(agent, bindingType));

                TrySendBindingMetric(bindingType, agent);
            });
        }

        private static void TrySendBindingMetric(Type bindingType, IAgent agent)
        {
            // this will work for both custom and MS bindings
            var sendMetric = false;
            lock (_bindingLock)
            {
                if (!_bindingsSent.Contains(bindingType))
                {
                    _bindingsSent.Add(bindingType);
                    sendMetric = true;
                }
            }

            if (sendMetric)
            {
                if (!SystemBindingTypes.Contains(bindingType))
                {
                    agent.GetExperimentalApi().RecordSupportabilityMetric("WCFClient/BindingType/CustomBinding");
                }
                else
                {
                    agent.GetExperimentalApi().RecordSupportabilityMetric($"WCFClient/BindingType/{bindingType.Name}");
                }
            }
        }
    }

    public class NewRelicClientMessageInspector : IClientMessageInspector
    {
        private const string AppDataHttpHeader = "X-NewRelic-App-Data";
        private IAgent _agent;
        private Type _bindingType;

        public NewRelicClientMessageInspector(IAgent agent, Type bindingType)
        {
            _agent = agent;
            _bindingType = bindingType;
        }

        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
            var typedCorrelationState = correlationState as CorrelationState;


            string headerValue = null;
            if (reply.Properties.TryGetValue(HttpResponseMessageProperty.Name, out var httpResponseObject))
            {
                var httpResponse = httpResponseObject as HttpResponseMessageProperty;
                headerValue = httpResponse.Headers[AppDataHttpHeader];

                var externalSegmentData = typedCorrelationState?.Segment?.GetExperimentalApi()?.SegmentData as IExternalSegmentData;
                externalSegmentData?.SetHttpStatus((int)httpResponse.StatusCode);
            }

            if (string.IsNullOrEmpty(headerValue))
            {
                var headerIndex = reply.Headers.FindHeader(AppDataHttpHeader, "");
                headerValue = headerIndex >= 0 ? reply.Headers.GetHeader<string>(headerIndex) : null;
            }

            if (correlationState != null && !string.IsNullOrEmpty(headerValue))
            {
                typedCorrelationState.Transaction.ProcessInboundResponse(new[] { new KeyValuePair<string, string>(AppDataHttpHeader, headerValue) }, typedCorrelationState?.Segment);
            }
        }

        private void SetHeaders (HttpRequestMessageProperty carrier, string key, string value)
        {
            // 'Set' will replace an existing value
            carrier.Headers?.Set(key, value);
        }

        private void SetHeaders(Message carrier, string key, string value)
        {
            // 'Set' will replace an existing value
            carrier.Headers.Add(MessageHeader.CreateHeader(key, string.Empty, value));
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            var transactionWrapperApi = _agent.CurrentTransaction;
            var correlationState = new CorrelationState(transactionWrapperApi, transactionWrapperApi.CurrentSegment);
            if (!correlationState.Transaction.IsValid || !correlationState.Segment.IsExternal)
            {
                return null;
            }

            try
            {


                if (_bindingType == typeof(NetTcpBinding))
                {
                    _agent.CurrentTransaction.InsertDistributedTraceHeaders(request, SetHeaders);
                }
                else
                {
                    HttpRequestMessageProperty httpRequestMessage;
                    if (request.Properties.TryGetValue(HttpRequestMessageProperty.Name, out object httpRequestMessageObject))
                    {
                        httpRequestMessage = httpRequestMessageObject as HttpRequestMessageProperty;
                    }
                    else
                    {
                        httpRequestMessage = new HttpRequestMessageProperty();
                        request.Properties.Add(HttpRequestMessageProperty.Name, httpRequestMessage);
                    }

                    _agent.CurrentTransaction.InsertDistributedTraceHeaders(httpRequestMessage, SetHeaders);
                }
            }
            catch (Exception ex)
            {
                _agent.HandleWrapperException(ex);
            }

            return correlationState;
        }

        public class CorrelationState
        {
            public CorrelationState(ITransaction transaction, ISegment segment)
            {
                Transaction = transaction;
                Segment = segment;
            }

            public ITransaction Transaction { get; }
            public ISegment Segment { get; }
        }
    }

    public class NewRelicEndpointBehavior : IEndpointBehavior
    {
        private readonly IAgent _agent;
        private readonly Type _bindingType;

        public NewRelicEndpointBehavior(IAgent agent, Type bindingType)
        {
            _bindingType = bindingType;
            _agent = agent;
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            var inspector = new NewRelicClientMessageInspector(_agent, _bindingType);
            clientRuntime.ClientMessageInspectors.Add(inspector);
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
        }

        public void Validate(ServiceEndpoint endpoint)
        {
        }
    }
}
