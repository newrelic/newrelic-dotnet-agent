﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Providers.Wrapper.Asp35.Shared;

namespace NewRelic.Providers.Wrapper.Asp35.IntegratedPipeline
{
	public class ExecuteStepWrapper : IWrapper
	{
		public bool IsTransactionRequired => false;

		public static class Statics
		{
			#region private static readonly IEnumerable<String> PossibleEvents
			[NotNull]
			public static readonly IEnumerable<String> PossibleEvents = new List<String>
			{
				"BeginRequest",
				"AuthenticateRequest",
				"AuthorizeRequest",
				"ResolveRequestCache",
				"MapRequestHandler",
				"AcquireRequestState",
				"PreExecuteRequestHandler",
				"ExecuteRequestHandler",
				"ReleaseRequestState",
				"UpdateRequestCache",
				"LogRequest",
				"EndRequest",
				"SendResponse",
			};
			#endregion

			/// <summary>
			/// micah: Apparently, MyEnum.ToString() is an expensive operation that requires reflection, though not in the place you would expect.  
			/// Now, a dictionary is created that contains all of the mappings so instead of doing reflection we just have to do a small dictionary lookup.
			/// </summary>
			[NotNull]
			public static IDictionary<RequestNotification, String> RequestNotificationToStringMap
			{
				get
				{
					if (_requestNotificationToString == null)
						_requestNotificationToString = Enum.GetValues(typeof(RequestNotification))
							.Cast<RequestNotification>()
							.ToDictionary(requestNotification => requestNotification, requestNotification => requestNotification.ToString());

					return _requestNotificationToString;
				}
			}
			private static IDictionary<RequestNotification, String> _requestNotificationToString;
		}

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "System.Web", typeName: "System.Web.HttpApplication", methodName: "ExecuteStep");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			if (!HttpRuntime.UsingIntegratedPipeline)
				return Delegates.NoOp;

			var httpApplication = (HttpApplication)instrumentedMethodCall.MethodCall.InvocationTarget;
			if (httpApplication == null)
				throw new NullReferenceException("httpApplication");

			var httpContext = httpApplication.Context;
			if (httpContext == null)
				throw new NullReferenceException("httpContext");

			var requestNotification = Statics.RequestNotificationToStringMap[httpContext.CurrentNotification];
			var lastRequestNotification = httpContext.Items[HttpContextActions.HttpContextSegmentTypeKey] as String;
			if (requestNotification == lastRequestNotification)
				return Delegates.NoOp;

			// if there is no transaction or segment yet then this will do nothing
			var segment = agentWrapperApi.CastAsSegment(httpContext.Items[HttpContextActions.HttpContextSegmentKey]);
			httpContext.Items[HttpContextActions.HttpContextSegmentKey] = null;
			httpContext.Items[HttpContextActions.HttpContextSegmentTypeKey] = null;
			segment.End();

			transaction = TryCreateTransaction(agentWrapperApi, httpContext, requestNotification);
			segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, requestNotification);

			httpContext.Items[HttpContextActions.HttpContextSegmentKey] = segment;
			httpContext.Items[HttpContextActions.HttpContextSegmentTypeKey] = requestNotification;

			return Delegates.NoOp;
		}

		private ITransaction TryCreateTransaction([NotNull] IAgentWrapperApi agentWrapperApi, [NotNull] HttpContext httpContext, String requestNotification)
		{
			// MapRequestHandler is always called so if we make it past that without having already started a transaction then don't start one since we already missed too much.  This is likely to occur during startup when the transaction service spins up half way through a request.
			var earlyEnoughInTransactionLifecycleToCreate = Statics.PossibleEvents
				.TakeWhile(@event => @event != "AcquireRequestState")
				.Where(@event => @event == requestNotification)
				.Any();
			if (!earlyEnoughInTransactionLifecycleToCreate)
				return agentWrapperApi.CurrentTransaction;

			Action onCreate = () =>
			{
				HttpContextActions.TransactionStartup(agentWrapperApi, httpContext);
			};
			return agentWrapperApi.CreateWebTransaction(WebTransactionType.ASP, "Integrated Pipeline", true, onCreate);
		}
	}
}
