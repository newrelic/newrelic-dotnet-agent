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
		private readonly IAgent _agent;

		public WrapPipelineMiddleware(RequestDelegate next, IAgent agent)
		{
			_next = next;
			_agent = agent;
		}

		public async Task Invoke(HttpContext context)
		{
			ITransaction transaction = null;
			ISegment segment = null;

			if (context.Request.Method == "OPTIONS")
			{
				// Don't create a transaction in this case to avoid MGIs associated with CORS pre-flight requests
				await _next(context);
				return;
			}

			try
			{
				transaction = SetupTransaction(context.Request);
				transaction.AttachToAsync(); //Important that this is called from an Invoke method that has the async keyword.
				transaction.DetachFromPrimary(); //Remove from thread-local type storage

				segment = SetupSegment(transaction, context);

				ProcessHeaders(context);

				context.Response.OnStarting(SetOutboundTracingDataAsync);
			}
			catch (Exception ex)
			{
				_agent.SafeHandleException(ex);
			}

			try
			{
				await _next(context);
				EndTransaction(segment, transaction, context, null);
			}
			catch (Exception ex)
			{
				EndTransaction(segment, transaction, context, ex);
				throw; //throw here to maintain call stack. 
			}

			Task SetOutboundTracingDataAsync()
			{
				TryWriteResponseHeaders(context, transaction);
				return Task.CompletedTask;
			}
		}

		private void EndTransaction(ISegment segment, ITransaction transaction, HttpContext context, Exception appException)
		{
			try
			{
				var responseStatusCode = context.Response.StatusCode;

				//We only keep 1 error per transaction so we are prioritizing the error that made its way
				//all the way to our middleware over the error caught by the ExceptionHandlerMiddleware.
				//It's possible that the 2 errors are the same under certain circumstances.
				if (appException != null)
				{
					transaction.NoticeError(appException);

					//Looks like we won't accurately notice that a 500 is going to be returned for exception cases,
					//because that appears to be handled at the  web host level or server (kestrel) level 
					responseStatusCode = 500;
				}
				else
				{
					var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
					if (exceptionHandlerFeature != null)
					{
						transaction.NoticeError(exceptionHandlerFeature.Error);
					}
				}

				if (responseStatusCode >= 400)
				{
					//Attempt low-priority transaction name to reduce chance of metric grouping issues.
					transaction.SetWebTransactionName(WebTransactionType.StatusCode, $"{responseStatusCode}", TransactionNamePriority.StatusCode);
				}

				segment.End();

				transaction.SetHttpResponseStatusCode(responseStatusCode);
				transaction.End();
			}
			catch (Exception ex)
			{
				_agent.SafeHandleException(ex);
			}
		}

		private ISegment SetupSegment(ITransaction transaction, HttpContext context)
		{
			// Seems like it would be cool to not require all of this for a segment??? 
			var method = new Method(typeof(WrapPipelineMiddleware), nameof(Invoke), nameof(context));
			var methodCall = new MethodCall(method, this, new object[] { context });

			var segment = transaction.StartTransactionSegment(methodCall, "Middleware Pipeline");
			return segment;
		}

		private ITransaction SetupTransaction(HttpRequest request)
		{
			var path = request.Path.Value;
			path = "/".Equals(path) ? "ROOT" : path.Substring(1);

			var transaction = _agent.CreateWebTransaction(WebTransactionType.ASP, path);

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

			_agent.ProcessInboundRequest(headers, TransportType.HTTP, contentLength);
		}

		private void TryWriteResponseHeaders(HttpContext httpContext, ITransaction transaction)
		{
			try
			{
				var headers = transaction.GetResponseMetadata();

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
				_agent.SafeHandleException(ex);
			}
		}
	}
}