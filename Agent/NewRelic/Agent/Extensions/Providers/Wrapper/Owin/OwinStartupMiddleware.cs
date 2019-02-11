using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using Microsoft.Owin;
using System.Linq;

namespace NewRelic.Providers.Wrapper.Owin
{
	internal class OwinStartupMiddleware : OwinMiddleware
	{
		private readonly OwinMiddleware _next;
		private readonly IAgentWrapperApi _agentWrapperApi;

		public OwinStartupMiddleware(OwinMiddleware next, IAgentWrapperApi agentWrapperApi)
			: base(next)
		{
			_next = next;
			_agentWrapperApi = agentWrapperApi;
		}

		public override async Task Invoke(IOwinContext context)
		{
			ISegment segment = null;
			ITransactionWrapperApi transactionWrapperApi = null;

			try
			{
				transactionWrapperApi = SetupTransaction(context.Request);
				transactionWrapperApi.AttachToAsync();
				transactionWrapperApi.DetachFromPrimary();

				segment = SetupSegment(transactionWrapperApi, context);

				ProcessHeaders(context);

				context.Response.OnSendingHeaders(SetOutboundTracingDataAsync, null);
			}
			catch (Exception ex)
			{
				_agentWrapperApi.SafeHandleException(ex);
			}

			try
			{
				await _next.Invoke(context);
				EndTransaction(segment, transactionWrapperApi, context, null);
			}
			catch (Exception ex)
			{
				EndTransaction(segment, transactionWrapperApi, context, ex);
				throw; //throw here to maintain call stack. 
			}

			void SetOutboundTracingDataAsync(object state)
			{
				TryWriteResponseHeaders(context, transactionWrapperApi);
			}
		}

		private ITransactionWrapperApi SetupTransaction(IOwinRequest request)
		{
			var path = request.Path.Value;
			path = "/".Equals(path) ? "ROOT" : path.Substring(1);

			var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Custom, path);

			transaction.SetUri(string.IsNullOrEmpty(request.Path.Value) ? "/Unknown" : request.Path.Value);

			if (request.QueryString.HasValue)
			{
				var parameters = new Dictionary<string, string>();
				foreach (var keyValuePair in request.Query)
				{
					parameters.Add(keyValuePair.Key, ConvertQueryStringValueToSingleValue(keyValuePair.Value));
				}

				transaction.SetRequestParameters(parameters);
			}

			return transaction;
		}

		private string ConvertQueryStringValueToSingleValue(string[] values)
		{
			if (values == null || values.Length == 0) return null;
			if (values.Length == 1) return values[0];
			else return string.Join(",", values);
		}

		private void ProcessHeaders(IOwinContext owinContext)
		{
			var headers = owinContext.Request.Headers.Select(header => new KeyValuePair<string, string>(header.Key, header.Value[0]));

			owinContext.Request.Headers.TryGetValue("Content-Length", out var contentLengthValue);
			long contentLength = default(long);
			bool parsedContentLength = false;
			if (contentLengthValue != null && contentLengthValue.Length > 0)
			{
				parsedContentLength = long.TryParse(contentLengthValue[0], out contentLength);
			}
			
			_agentWrapperApi.ProcessInboundRequest(headers, TransportType.HTTP, parsedContentLength ? contentLength : (long?) null);
		}

		private ISegment SetupSegment(ITransactionWrapperApi transactionWrapperApi, IOwinContext owinContext)
		{
			var method = new Method(typeof(OwinStartupMiddleware), nameof(Invoke), nameof(owinContext));
			var methodCall = new MethodCall(method, this, new object[] {owinContext});

			var segment = transactionWrapperApi.StartTransactionSegment(methodCall, "Owin Middleware Pipeline");
			return segment;
		}

		private void EndTransaction(ISegment segment, ITransactionWrapperApi transactionWrapperApi, IOwinContext owinContext, Exception appException)
		{
			try
			{
				var responseStatusCode = owinContext.Response.StatusCode;

				if (appException != null)
				{
					transactionWrapperApi.NoticeError(appException);

					//Response code may not be 500 for exception cases,
					//because that appears to be handled at the  web host or server level
					responseStatusCode = 500;
				}

				if (responseStatusCode >= 400)
				{
					//Attempt low-priority transaction name to reduce chance of metric grouping issues.
					transactionWrapperApi.SetWebTransactionName(WebTransactionType.StatusCode, $"{responseStatusCode}", TransactionNamePriority.StatusCode);
				}

				segment.End();

				transactionWrapperApi.SetHttpResponseStatusCode(responseStatusCode);
				transactionWrapperApi.End();
			}
			catch (Exception ex)
			{
				_agentWrapperApi.SafeHandleException(ex);
			}
		}

		private void TryWriteResponseHeaders(IOwinContext owinContext, ITransactionWrapperApi transactionWrapperApi)
		{
			try
			{
				var headers = transactionWrapperApi.GetResponseMetadata();

				foreach (var header in headers)
				{
					if (header.Key != null && header.Value != null)
					{
						owinContext.Response.Headers.Add(header.Key, new[] { header.Value });
					}
				}
			}
			catch (Exception ex)
			{
				_agentWrapperApi.SafeHandleException(ex);
			}
		}
	}
}