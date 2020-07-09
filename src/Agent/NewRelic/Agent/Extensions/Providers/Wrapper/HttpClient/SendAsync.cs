using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.HttpClient
{
	public class SendAsync : IWrapper
	{
		public const String InstrumentedTypeName = "System.Net.Http.HttpClient";

		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			if (method.MatchesAny(assemblyName: "System.Net.Http", typeName: InstrumentedTypeName, methodName: "SendAsync"))
			{
				return NewRelic.Providers.Wrapper.WrapperUtilities.WrapperUtils.LegacyAspPipelineIsPresent()
					? new CanWrapResponse(false, NewRelic.Providers.Wrapper.WrapperUtilities.WrapperUtils.LegacyAspPipelineNotSupportedMessage("System.Net.Http", "System.Net.Http.HttpClient", method.MethodName))
					: new CanWrapResponse(true);
			}
			else
			{
				return new CanWrapResponse(false);
			}
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			if (instrumentedMethodCall.IsAsync)
			{
				transaction.AttachToAsync();
			}

			var httpRequestMessage = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<HttpRequestMessage>(0);
			var httpClient = (System.Net.Http.HttpClient) instrumentedMethodCall.MethodCall.InvocationTarget;
			var uri = TryGetAbsoluteUri(httpRequestMessage, httpClient);
			if (uri == null)
			{
				// It is possible for RequestUri to be null, but if it is then SendAsync method will eventually throw (which we will see). It would not be valuable to throw another exception here.
				return Delegates.NoOp;
			}
			
			// We cannot rely on SerializeHeadersWrapper to attach the headers because it is called on a thread that does not have access to the transaction
			TryAttachHeadersToRequest(agentWrapperApi, httpRequestMessage);

			var method = (httpRequestMessage.Method != null ? httpRequestMessage.Method.Method : "<unknown>") ?? "<unknown>";
			var segment = agentWrapperApi.CurrentTransaction.StartExternalRequestSegment(instrumentedMethodCall.MethodCall, uri, method);

			return Delegates.GetDelegateFor<Task<HttpResponseMessage>>(
				onFailure: ex =>
				{
					segment.End();
				}, onSuccess: task =>
				{
					segment.RemoveSegmentFromCallStack();

					if (task == null)
						return;

					var context = SynchronizationContext.Current;
					if (context != null)
					{
						task.ContinueWith(responseTask => agentWrapperApi.HandleExceptions(() =>
						{
							TryProcessResponse(agentWrapperApi, responseTask, transaction, segment);
							segment.End();
						}), TaskScheduler.FromCurrentSynchronizationContext());
					}
					else
					{
						task.ContinueWith(responseTask => agentWrapperApi.HandleExceptions(() =>
						{
							TryProcessResponse(agentWrapperApi, responseTask, transaction, segment);
							segment.End();
						}), TaskContinuationOptions.ExecuteSynchronously);
					}
				});
		}

		[CanBeNull]
		private static Uri TryGetAbsoluteUri([NotNull] HttpRequestMessage httpRequestMessage, [NotNull] System.Net.Http.HttpClient httpClient)
		{
			// If RequestUri is specified and it is an absolute URI then we should use it
			if (httpRequestMessage.RequestUri?.IsAbsoluteUri == true)
				return httpRequestMessage.RequestUri;

			// If RequestUri is specified but isn't absolute then we need to combine it with the BaseAddress, as long as the BaseAddress is an absolute URI
			if (httpRequestMessage.RequestUri?.IsAbsoluteUri == false && httpClient.BaseAddress?.IsAbsoluteUri == true)
				return new Uri(httpClient.BaseAddress, httpRequestMessage.RequestUri);

			// If only BaseAddress is specified and it is an absolute URI then we can use it instead
			if (httpRequestMessage.RequestUri == null && httpClient.BaseAddress?.IsAbsoluteUri == true)
				return httpClient.BaseAddress;

			// In all other cases we cannot construct a valid absolute URI
			return null;
		}

		private static void TryAttachHeadersToRequest([NotNull] IAgentWrapperApi agentWrapperApi, [NotNull] HttpRequestMessage httpRequestMessage)
		{
			try
			{
				var headers = agentWrapperApi.CurrentTransaction.GetRequestMetadata()
					.Where(header => header.Key != null);

				foreach (var header in headers)
				{
					// "Add" will not replace an existing value, so we must remove it first
					httpRequestMessage.Headers?.Remove(header.Key);
					httpRequestMessage.Headers?.Add(header.Key, header.Value);
				}
			}
			catch (Exception ex)
			{
				agentWrapperApi.HandleWrapperException(ex);
			}
		}

		private static void TryProcessResponse([NotNull] IAgentWrapperApi agentWrapperApi, [CanBeNull] Task<HttpResponseMessage> response, [NotNull] ITransaction transaction, [CanBeNull] ISegment segment)
		{
			try
			{
				if (!ValidTaskResponse(response) || (segment == null))
				{
					return;
				}

				var headers = response?.Result?.Headers?.ToList();
				if (headers == null)
					return;

				var flattenedHeaders = headers.Select(Flatten);

				transaction.ProcessInboundResponse(flattenedHeaders, segment);
			}
			catch (Exception ex)
			{
				agentWrapperApi.HandleWrapperException(ex);
			}
		}

		private static Boolean ValidTaskResponse([CanBeNull] Task<HttpResponseMessage> response)
		{
			return (response?.Status == TaskStatus.RanToCompletion);
		}

		private static KeyValuePair<String, String> Flatten(KeyValuePair<String, IEnumerable<String>> header)
		{
			var key = header.Key;
			var values = header.Value ?? Enumerable.Empty<String>();

			// According to RFC 2616 (http://www.w3.org/Protocols/rfc2616/rfc2616-sec4.html#sec4.2), multi-valued headers can be represented as a single comma-delimited list of values
			var flattenedValues = String.Join(",", values);

			return new KeyValuePair<String, String>(key, flattenedValues);
		}
	}
}