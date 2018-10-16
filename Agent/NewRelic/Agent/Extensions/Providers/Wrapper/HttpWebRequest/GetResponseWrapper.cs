using System;
using System.Net;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions.Collections;

namespace NewRelic.Providers.Wrapper.HttpWebRequest
{
	public class GetResponseWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "System", typeName: "System.Net.HttpWebRequest", methodName: "GetResponse");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			var httpWebRequest = instrumentedMethodCall.MethodCall.InvocationTarget as System.Net.HttpWebRequest;
			if(httpWebRequest == null)
				throw new NullReferenceException(nameof(httpWebRequest));

			var uri = httpWebRequest.RequestUri;
			if (uri == null)
				return Delegates.NoOp;

			var method = httpWebRequest.Method ?? "<unknown>";
			var segment = transactionWrapperApi.StartExternalRequestSegment(instrumentedMethodCall.MethodCall, uri, method);
			segment.MakeCombinable();

			return Delegates.GetDelegateFor<HttpWebResponse>(
				onSuccess: response => TryProcessResponse(response, transactionWrapperApi, segment),
				onFailure: exception => TryProcessResponse((exception as WebException)?.Response, transactionWrapperApi, segment),
				onComplete: segment.End
				);
		}

		private static void TryProcessResponse([CanBeNull] WebResponse response, [NotNull] ITransactionWrapperApi transactionWrapperApi, [CanBeNull] ISegment segment)
		{
			if (segment == null)
			{
				return;
			}

			var headers = response?.Headers?.ToDictionary();
			if (headers == null)
			{
				return;
			}

			transactionWrapperApi.ProcessInboundResponse(headers, segment);
		}
	}
}
