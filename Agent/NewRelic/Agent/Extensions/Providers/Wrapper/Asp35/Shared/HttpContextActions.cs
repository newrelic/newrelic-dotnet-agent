using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using NewRelic.SystemExtensions.Collections;

namespace NewRelic.Providers.Wrapper.Asp35.Shared
{
	public static class HttpContextActions
	{
		public const String HttpContextSegmentKey = "NewRelic.Asp.HttpContextSegmentKey";
		public const String HttpContextSegmentTypeKey = "NewRelic.Asp.HttpContextSegmentTypeKey";

		// System.Web.HttpResponseStreamFilterSink is an internal type so we cannot reference it directly
		public static readonly Type HttpResponseStreamFilterSinkType = typeof (HttpResponse).Assembly.GetType("System.Web.HttpResponseStreamFilterSink");

		[CanBeNull]
		private static Func<HttpWorkerRequest, DateTime> _getStartTime;

		[NotNull]
		private static Func<HttpWorkerRequest, DateTime> GetStartTime
		{
			get
			{
				return _getStartTime ?? (_getStartTime = VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<HttpWorkerRequest, DateTime>("GetStartTime"));
			}
		}

		public static void TransactionStartup([NotNull] IAgentWrapperApi agentWrapperApi, [NotNull] HttpContext httpContext)
		{
			SetFilterHack(httpContext);
			StoreQueueTime(agentWrapperApi, httpContext);
			StoreUrls(agentWrapperApi, httpContext);
			NameTransaction(agentWrapperApi, httpContext);
			ProcessHeaders(agentWrapperApi, httpContext);
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
		private static void SetFilterHack([NotNull] HttpContext httpContext)
		{
			var filter = httpContext.Response.Filter;
			if (filter != null && filter.GetType() != HttpResponseStreamFilterSinkType)
				return;

			httpContext.Response.Filter = null;
		}

		public static void TransactionShutdown([NotNull] IAgentWrapperApi agentWrapperApi, [NotNull] HttpContext httpContext)
		{
			StoreRequestParameters(agentWrapperApi, httpContext);
			TryWriteResponseHeaders(agentWrapperApi, httpContext);
			SetStatusCode(agentWrapperApi, httpContext);
		}

		private static void StoreQueueTime([NotNull] IAgentWrapperApi agentWrapperApi, [NotNull] HttpContext httpContext)
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
			agentWrapperApi.CurrentTransaction.SetQueueTime(inQueueTimeSpan);
		}

		private static void StoreUrls([NotNull] IAgentWrapperApi agentWrapperApi, [NotNull] HttpContext httpContext)
		{
			var transaction = agentWrapperApi.CurrentTransaction;

			var requestPath = RequestPathRetriever.TryGetRequestPath(httpContext.Request);

			var requestUrl = RequestUrlRetriever.TryGetRequestUrl(httpContext.Request, () => requestPath);
			if (requestUrl != null)
			{
				transaction.SetUri(requestUrl.AbsoluteUri);
				transaction.SetOriginalUri(requestUrl.AbsoluteUri);
			}

			var referrerUri = TryGetReferrerUri(httpContext.Request);
			if (referrerUri != null)
			{
				transaction.SetReferrerUri(referrerUri.AbsoluteUri);
			}
		}

		[CanBeNull]
		private static Uri TryGetReferrerUri([NotNull] HttpRequest request)
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

		private static void StoreRequestParameters([NotNull] IAgentWrapperApi agentWrapperApi, [NotNull] HttpContext httpContext)
		{
			var parameters = QueryStringRetriever.TryGetQueryStringAsDictionary(httpContext.Request, agentWrapperApi)
				?? Enumerable.Empty<KeyValuePair<String, String>>();
			agentWrapperApi.CurrentTransaction.SetRequestParameters(parameters);
		}

		private static void NameTransaction([NotNull] IAgentWrapperApi agentWrapperApi, [NotNull] HttpContext httpContext)
		{
			agentWrapperApi.CurrentTransaction.SetWebTransactionNameFromPath(WebTransactionType.ASP, httpContext.Request.Path);
		}

		private static void ProcessHeaders([NotNull] IAgentWrapperApi agentWrapperApi, [NotNull] HttpContext httpContext)
		{
			var headers = httpContext.Request.Headers.ToDictionary();
			var contentLength = httpContext.Request.ContentLength;
			agentWrapperApi.ProcessInboundRequest(headers, contentLength);
		}

		private static void TryWriteResponseHeaders([NotNull] IAgentWrapperApi agentWrapperApi, [NotNull] HttpContext httpContext)
		{
			var headers = agentWrapperApi.CurrentTransaction.GetResponseMetadata();

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
			catch (HttpException e)
			{
				Log.Warn($"Could not add HTTP response headers, response marked as already sent: {e.ToString()}");
			}
			catch (Exception ex)
			{
				agentWrapperApi.HandleWrapperException(ex);
			}
		}

		private static void SetStatusCode([NotNull] IAgentWrapperApi agentWrapperApi, [NotNull] HttpContext httpContext)
		{
			var statusCode = httpContext.Response.StatusCode;
			var subStatusCode = TryGetSubStatusCode(httpContext);
			agentWrapperApi.CurrentTransaction.SetHttpResponseStatusCode(statusCode, subStatusCode);
		}

		[CanBeNull]
		private static Int32? TryGetSubStatusCode([NotNull] HttpContext httpContext)
		{
			// Oddly, SubStatusCode will throw in classic pipeline mode
			if (!HttpRuntime.UsingIntegratedPipeline)
				return null;

			return httpContext.Response.SubStatusCode;
		}
	}
}
