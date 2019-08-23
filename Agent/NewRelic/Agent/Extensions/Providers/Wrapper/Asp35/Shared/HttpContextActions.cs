using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using NewRelic.SystemExtensions.Collections;

namespace NewRelic.Providers.Wrapper.Asp35.Shared
{
	public static class HttpContextActions
	{
		public const string HttpContextSegmentKey = "NewRelic.Asp.HttpContextSegmentKey";
		public const string HttpContextSegmentTypeKey = "NewRelic.Asp.HttpContextSegmentTypeKey";

		// System.Web.HttpResponseStreamFilterSink is an internal type so we cannot reference it directly
		public static readonly Type HttpResponseStreamFilterSinkType = typeof (HttpResponse).Assembly.GetType("System.Web.HttpResponseStreamFilterSink");

		private static Func<HttpWorkerRequest, DateTime> _getStartTime;

		private static Func<HttpWorkerRequest, DateTime> GetStartTime
		{
			get
			{
				return _getStartTime ?? (_getStartTime = VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<HttpWorkerRequest, DateTime>("GetStartTime"));
			}
		}

		public static void TransactionStartup(IAgent agent, HttpContext httpContext)
		{
			SetFilterHack(httpContext);
			StoreQueueTime(agent, httpContext);
			StoreUrls(agent, httpContext);
			NameTransaction(agent, httpContext);
			ProcessHeaders(agent, httpContext);
		}

		/// <summary>
		/// This method is a somewhat complicated hack designed to work around some strange behavior in ASP.NET.
		/// 
		/// The problem that is being solved is that, under certain conditions (such as default document redirects in WebForms in integrated pipeline mode in .NET 3.5 or lower), the HtppResponse filter that is attached via FilterWrapper is being ignored.
		/// 
		/// The root cause appears to be that there are some important side effects of setting the HttpResponse.Filter property that need to happen early in the pipeline. However, we cannot simply attach our filter earlier because another bug in ASP.NET will cause WebResources (and perhaps other content streams) to become corrupted if the filters are attached too early.
		/// 
		/// The solution that this method provides is to set the filter to null early in the pipeline, which will trigger the necessary side-effects without actually attaching a filter. We need to be careful to do this only if another filter has not already been attached. Due to (yet again) more strange behavior in ASP.NET, the only way to tell if a filter has already been attached is to check if the filter is currently a System.Web.HttpResponseStreamFilterSink (which is the default filter).
		/// </summary>
		/// <param name="httpContext"></param>
		private static void SetFilterHack(HttpContext httpContext)
		{
			var filter = httpContext.Response.Filter;
			if (filter != null && filter.GetType() != HttpResponseStreamFilterSinkType)
				return;

			httpContext.Response.Filter = null;
		}

		public static void TransactionShutdown(IAgent agent, HttpContext httpContext)
		{
			StoreRequestParameters(agent, httpContext);
			SetStatusCode(agent, httpContext);
			TryWriteResponseHeaders(agent, httpContext);
		}

		private static void StoreQueueTime(IAgent agent, HttpContext httpContext)
		{
			var now = DateTime.UtcNow;

			// TODO: This will restore the code back to sending queue time using the difference between when the item was pulled 
			// off of the queue to when it was actually being processed. This is because the HttpWorkerRequest.GetStartTime value gets recet when the item is pulled off.
			// We need to possibly create a tracer for the System.Web.HttpRuntime.CalculateWaitTimeAndUpdatePerfCounter(HttpWorkerRequest wr)
			// Store the queue time value provided there in the current local thread then use it here instead
			var service = (httpContext as IServiceProvider).GetService(typeof(HttpWorkerRequest));
			var workerRequest = service as HttpWorkerRequest;
			if (workerRequest == null)
				return;

			var workerRequestStartTime = GetStartTime(workerRequest);
			var inQueueTimeSpan = now - workerRequestStartTime;
			agent.CurrentTransaction.SetQueueTime(inQueueTimeSpan);
		}

		private static void StoreUrls(IAgent agent, HttpContext httpContext)
		{
			var transaction = agent.CurrentTransaction;

			var requestPath = RequestPathRetriever.TryGetRequestPath(httpContext.Request);

			var requestUrl = RequestUrlRetriever.TryGetRequestUrl(httpContext.Request, () => requestPath);
			if (requestUrl != null)
			{
				transaction.SetUri(requestUrl.AbsolutePath);
				transaction.SetOriginalUri(requestUrl.AbsolutePath);
			}

			var referrerUri = TryGetReferrerUri(httpContext.Request);
			if (referrerUri != null)
			{
				transaction.SetReferrerUri(referrerUri.AbsolutePath);
			}
		}

		private static Uri TryGetReferrerUri(HttpRequest request)
		{
			try
			{
				// HttpRequest.UrlReferrer will throw if the referrer is invalid (which is easy to reproduce since anyone can write anything they want in the HTTP Referer header).
				// Note that the decompiled code for System.Web appears to use try/catches that would prevent this exception from escaping, but it happens nonetheless.
				return request.UrlReferrer;
			}
			catch
			{
				return null;
			}
		}

		private static void StoreRequestParameters(IAgent agent, HttpContext httpContext)
		{
			var parameters = QueryStringRetriever.TryGetQueryStringAsDictionary(httpContext.Request, agent)
				?? Enumerable.Empty<KeyValuePair<string, string>>();
			agent.CurrentTransaction.SetRequestParameters(parameters);
		}

		private static void NameTransaction(IAgent agent, HttpContext httpContext)
		{
			agent.CurrentTransaction.SetWebTransactionNameFromPath(WebTransactionType.ASP, httpContext.Request.Path);
		}

		private static void ProcessHeaders(IAgent agent, HttpContext httpContext)
		{
			var headers = httpContext.Request.Headers.ToDictionary();
			var contentLength = httpContext.Request.ContentLength;
			agent.ProcessInboundRequest(headers, TransportType.HTTP, contentLength);
		}

		private static void TryWriteResponseHeaders(IAgent agent, HttpContext httpContext)
		{
			var headers = agent.CurrentTransaction.GetResponseMetadata();

			try
			{
				foreach (var header in headers)
				{
					if (header.Key == null || header.Value == null)
						continue;

					// HttpResponse.Headers is not supported in classic pipeline (will throw a PlatformNotSupportedException), so use HttpResponse.AddHeader
					httpContext.Response.AddHeader(header.Key, header.Value);
				}
			}
			catch (HttpException)
			{
				// Swallow HttpExceptions that can be thrown if the response has already been sent (e.g. in case of a server redirect)
			}
			catch (Exception ex)
			{
				agent.HandleWrapperException(ex);
			}
		}

		private static void SetStatusCode(IAgent agent, HttpContext httpContext)
		{
			var statusCode = httpContext.Response.StatusCode;
			var subStatusCode = TryGetSubStatusCode(httpContext);
			agent.CurrentTransaction.SetHttpResponseStatusCode(statusCode, subStatusCode);
		}

		private static int? TryGetSubStatusCode(HttpContext httpContext)
		{
			// Oddly, SubStatusCode will throw in classic pipeline mode
			if (!HttpRuntime.UsingIntegratedPipeline)
				return null;

			return httpContext.Response.SubStatusCode;
		}
	}
}
