using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace NewRelic.Providers.Wrapper.Wcf3
{
	public class ChannelFactoryWrapper : IWrapper
	{
		public bool IsTransactionRequired => false;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
		{
			var method = instrumentedMethodInfo.Method;
			var canWrap = method.MatchesAny
			(
				assemblyName: "System.ServiceModel",
				typeName: "System.ServiceModel.ChannelFactory",
				methodName: "InitializeEndpoint"
			);
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
			return Delegates.GetDelegateFor(onComplete: () =>
			{
				var channelFactory = instrumentedMethodCall.MethodCall.InvocationTarget as ChannelFactory;
				channelFactory?.Endpoint.Behaviors.Add(new NewRelicEndpointBehavior(agent));
			});
		}
	}

	public class NewRelicClientMessageInspector : IClientMessageInspector
	{
		private IAgent _agent;
		private const string AppDataHttpHeader = "X-NewRelic-App-Data";

		public NewRelicClientMessageInspector(IAgent agent)
		{
			_agent = agent;
		}

		public void AfterReceiveReply(ref Message reply, object correlationState)
		{
			string headerValue = null;
			
			if(reply.Properties.TryGetValue(HttpResponseMessageProperty.Name, out var httpResponseObject))
			{
				var httpResponse = httpResponseObject as HttpResponseMessageProperty;
				headerValue = httpResponse.Headers[AppDataHttpHeader];
			}

			if (string.IsNullOrEmpty(headerValue))
			{
				var headerIndex = reply.Headers.FindHeader(AppDataHttpHeader, "");
				headerValue = headerIndex > 0 ? reply.Headers.GetHeader<string>(headerIndex) : null;
			}

			var typedCorrelationState = correlationState as CorrelationState;
			if (correlationState != null && !string.IsNullOrEmpty(headerValue))
			{
				typedCorrelationState.Transaction.ProcessInboundResponse(new[] { new KeyValuePair<string, string>(AppDataHttpHeader, headerValue) }, typedCorrelationState?.Segment);

				//TODO: Change the way WCF instrumentation ends segment so that it always end the external segment here to support TAP and EAP style calls. 
			}
		}

		public object BeforeSendRequest(ref Message request, IClientChannel channel)
		{
			var transactionWrapperApi = _agent.CurrentTransaction;
			var correlationState = new CorrelationState(transactionWrapperApi, transactionWrapperApi.CurrentSegment);

			if (!correlationState.Transaction.IsValid || !correlationState.Segment.IsExternal)
			{
				return null;
			}

			var headersToAdd = transactionWrapperApi.GetRequestMetadata();

			HttpRequestMessageProperty httpRequestMessage;
			object httpRequestMessageObject;
			if (request.Properties.TryGetValue(HttpRequestMessageProperty.Name, out httpRequestMessageObject))
			{
				httpRequestMessage = httpRequestMessageObject as HttpRequestMessageProperty;
			}
			else
			{
				httpRequestMessage = new HttpRequestMessageProperty();
				request.Properties.Add(HttpRequestMessageProperty.Name, httpRequestMessage);
			}

			foreach (var header in headersToAdd)
			{
				httpRequestMessage.Headers.Add(header.Key, header.Value);
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

		public NewRelicEndpointBehavior(IAgent agent)
		{
			_agent = agent;
		}

		public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
		{
		}

		public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
		{
			var inspector = new NewRelicClientMessageInspector(_agent);
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
