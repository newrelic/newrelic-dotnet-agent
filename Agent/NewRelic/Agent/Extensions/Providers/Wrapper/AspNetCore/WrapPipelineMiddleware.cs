using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AspNetCore
{
	/// <summary>
	/// This middleware is intended to be injected as the first executed item 
	/// for the ASP.NET Core pipeline. Need to be exra careful not to let any NR exceptions 
	/// bubble out to the client application.
	/// 
	/// This class is marked internal because it is not meant to be used by any agent dynamic type loading.
	/// </summary>
	internal class WrapPipelineMiddleware
	{
		private readonly RequestDelegate _next;
		private readonly IAgentWrapperApi _agentWrapperApi;

		public WrapPipelineMiddleware(RequestDelegate next, IAgentWrapperApi agentWrapperApi)
		{
			_next = next;
			_agentWrapperApi = agentWrapperApi;
		}

		public async Task Invoke(HttpContext context)
		{
			ITransactionWrapperApi transactionWrapperApi = null;
			ISegment segment = null;

			try
			{
				transactionWrapperApi = SetupTransaction(context.Request);
				transactionWrapperApi.AttachToAsync(); //Important that this is called from an Invoke method that has the async keyword.
				transactionWrapperApi.DetachFromPrimary(); //Remove from thread-local type storage

				segment = SetupSegment(transactionWrapperApi, context);

				ProcessHeaders(context);

				context.Response.OnStarting(SetOutboundTracingDataAsync);
			}
			catch (Exception ex)
			{
				_agentWrapperApi.SafeHandleException(ex);
			}

			try
			{
				await _next(context);
				EndTransaction(segment, transactionWrapperApi, context, null);
			}
			catch (Exception ex)
			{
				EndTransaction(segment, transactionWrapperApi, context, ex);
				throw; //throw here to maintain call stack. 
			}

			Task SetOutboundTracingDataAsync()
			{
				TryWriteResponseHeaders(context, transactionWrapperApi);
				return Task.CompletedTask;
			}
		}

		private void EndTransaction(ISegment segment, ITransactionWrapperApi transactionWrapperApi, HttpContext context, Exception appException)
		{
			try
			{
				var responseStatusCode = context.Response.StatusCode;

				//We only keep 1 error per transaction so we are prioritizing the error that made its way
				//all the way to our middleware over the error caught by the ExceptionHandlerMiddleware.
				//It's possible that the 2 errors are the same under certain circumstances.
				if (appException != null)
				{
					transactionWrapperApi.NoticeError(appException);

					//Looks like we won't accurately notice that a 500 is going to be returned for exception cases,
					//because that appears to be handled at the  web host level or server (kestrel) level 
					responseStatusCode = 500;
				}
				else
				{
					var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
					if (exceptionHandlerFeature != null)
					{
						transactionWrapperApi.NoticeError(exceptionHandlerFeature.Error);
					}
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

		private ISegment SetupSegment(ITransactionWrapperApi transactionWrapperApi, HttpContext context)
		{
			// Seems like it would be cool to not require all of this for a segment??? 
			var method = new Method(typeof(WrapPipelineMiddleware), nameof(Invoke), nameof(context));
			var methodCall = new MethodCall(method, this, new object[] { context });

			var segment = transactionWrapperApi.StartTransactionSegment(methodCall, "Middleware Pipeline");
			return segment;
		}

		private ITransactionWrapperApi SetupTransaction(HttpRequest request)
		{
			var path = request.Path.Value;
			path = "/".Equals(path) ? "ROOT" : path.Substring(1);

			var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.ASP, path);

			transaction.SetUri(request.Path);

			if (request.QueryString.HasValue)
			{
				var parameters = new Dictionary<string, string>();
				foreach (var keyValuePair in request.Query)
				{
					parameters.Add(keyValuePair.Key, keyValuePair.Value);
				}

				transaction.SetRequestParameters(parameters);
			}

			return transaction;
		}

		private void ProcessHeaders(HttpContext httpContext)
		{
			var headers = httpContext.Request.Headers.Select(header => new KeyValuePair<string, string>(header.Key, header.Value));
			var contentLength = httpContext.Request.ContentLength;

			_agentWrapperApi.ProcessInboundRequest(headers, "HTTP", contentLength);
		}

		private void TryWriteResponseHeaders(HttpContext httpContext, ITransactionWrapperApi transactionWrapperApi)
		{
			try
			{
				var headers = transactionWrapperApi.GetResponseMetadata();

				foreach (var header in headers)
				{
					if (header.Key != null && header.Value != null)
					{
						httpContext.Response.Headers.Add(header.Key, header.Value);
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