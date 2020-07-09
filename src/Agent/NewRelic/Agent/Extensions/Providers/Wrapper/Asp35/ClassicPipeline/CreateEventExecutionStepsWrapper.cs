﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Web;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Providers.Wrapper.Asp35.Shared;
using NewRelic.Reflection;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.Asp35.ClassicPipeline
{
	public class CreateEventExecutionStepsWrapper : IWrapper
	{
		public bool IsTransactionRequired => false;

		/// <summary>
		/// All of the statics for <see cref="CreateEventExecutionStepsWrapper"/> are stored here so that they won't be JIT'd when the type is first loaded. This is necessary because many of those statics reference types like HttpAplication that won't be available until dependencies are later loaded.
		/// </summary>
		private static class Statics
		{
			[NotNull]
			private static readonly Type HttpApplicationType = typeof(HttpApplication);

			[NotNull]
			public static readonly Func<HttpApplication, EventHandler, Object> CreateSyncEventExecutionStep = VisibilityBypasser.Instance.GenerateTypeFactory<HttpApplication, EventHandler>("System.Web", "System.Web.HttpApplication+SyncEventExecutionStep");

			#region private static readonly Dictionary<Object, String> EventIndexToString

			[NotNull]
			public static readonly Dictionary<Object, String> EventIndexToString = GetEventIndexMappings();

			private static Dictionary<Object, String> GetEventIndexMappings()
			{
				var eventIndexMappings = new Dictionary<Object, String>
				{
					{GetHttpApplicationField("EventDisposed"), "Disposed"},
					{GetHttpApplicationField("EventErrorRecorded"), "ErrorRecorded"},
					{GetHttpApplicationField("EventPreSendRequestHeaders"), "PreSendRequestHeaders"},
					{GetHttpApplicationField("EventPreSendRequestContent"), "PreSendRequestContent"},
					{GetHttpApplicationField("EventBeginRequest"), "BeginRequest"},
					{GetHttpApplicationField("EventAuthenticateRequest"), "AuthenticateRequest"},
					{GetHttpApplicationField("EventDefaultAuthentication"), "DefaultAuthentication"},
					{GetHttpApplicationField("EventPostAuthenticateRequest"), "PostAuthenticateRequest"},
					{GetHttpApplicationField("EventAuthorizeRequest"), "AuthorizeRequest"},
					{GetHttpApplicationField("EventPostAuthorizeRequest"), "PostAuthorizeRequest"},
					{GetHttpApplicationField("EventResolveRequestCache"), "ResolveRequestCache"},
					{GetHttpApplicationField("EventPostResolveRequestCache"), "PostResolveRequestCache"},
					{GetHttpApplicationField("EventMapRequestHandler"), "MapRequestHandler"},
					{GetHttpApplicationField("EventPostMapRequestHandler"), "PostMapRequestHandler"},
					{GetHttpApplicationField("EventAcquireRequestState"), "AcquireRequestState"},
					{GetHttpApplicationField("EventPostAcquireRequestState"), "PostAcquireRequestState"},
					{GetHttpApplicationField("EventPreRequestHandlerExecute"), "PreRequestHandlerExecute"},
					{GetHttpApplicationField("EventPostRequestHandlerExecute"), "PostRequestHandlerExecute"},
					{GetHttpApplicationField("EventReleaseRequestState"), "ReleaseRequestState"},
					{GetHttpApplicationField("EventPostReleaseRequestState"), "PostReleaseRequestState"},
					{GetHttpApplicationField("EventUpdateRequestCache"), "UpdateRequestCache"},
					{GetHttpApplicationField("EventPostUpdateRequestCache"), "PostUpdateRequestCache"},
					{GetHttpApplicationField("EventLogRequest"), "LogRequest"},
					{GetHttpApplicationField("EventPostLogRequest"), "PostLogRequest"},
					{GetHttpApplicationField("EventEndRequest"), "EndRequest"},
				};

				// In .NET 4.5 the "RequestCompleted" event was added
				var dotNet45MinVersion = new Version(4, 0, 30319, 17001);
				if (Environment.Version >= dotNet45MinVersion)
					eventIndexMappings.Add(GetHttpApplicationField("EventRequestCompleted"), "RequestCompleted");

				return eventIndexMappings;
			}

			#endregion


			[NotNull]
			private static Object GetHttpApplicationField([NotNull] String fieldName)
			{
				var field = HttpApplicationType
					.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
				if(field == null)
					throw new NullReferenceException("No HttpApplication field named " + fieldName);

				return field.GetValue(null) ?? new Object();
			}
		}

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "System.Web", typeName: "System.Web.HttpApplication", methodName: "CreateEventExecutionSteps");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			var httpApplication = (HttpApplication)instrumentedMethodCall.MethodCall.InvocationTarget;
			if (httpApplication == null)
				throw new NullReferenceException("invocationTarget");

			var eventIndex = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<Object>(0);
			var steps = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<ArrayList>(1);

			var eventName = Statics.EventIndexToString[eventIndex];
			if (eventName == null)
				throw new NullReferenceException("Could not find a valid eventName for index " + eventIndex);

			var beforeExecutionStep = GetBeforeExecutionStep(instrumentedMethodCall.MethodCall, agentWrapperApi, eventName, httpApplication);
			var afterExecutionStep = GetAfterExecutionStep(instrumentedMethodCall.MethodCall, agentWrapperApi, eventName, httpApplication);
			
			steps.Add(beforeExecutionStep);
			return Delegates.GetDelegateFor(() => steps.Add(afterExecutionStep));
		}

		private void BeforeEvent(MethodCall methodCall, [NotNull] IAgentWrapperApi agentWrapperApi, [NotNull] String eventName, [NotNull] HttpApplication httpApplication)
		{
			if (httpApplication == null)
				throw new ArgumentNullException("httpApplication");

			var httpContext = httpApplication.Context;
			if (httpContext == null)
				throw new NullReferenceException("httpApplication.Context");

			ITransaction transaction;
			if (eventName == "BeginRequest")
			{
				Action onCreate = () =>
				{
					HttpContextActions.TransactionStartup(agentWrapperApi, httpContext);
				};
				transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.ASP, "Classic Pipeline", true, onCreate);
			} else
			{
				transaction = agentWrapperApi.CurrentTransaction;
			}

			var segment = transaction.StartTransactionSegment(methodCall, eventName);
			httpContext.Items[HttpContextActions.HttpContextSegmentKey] = segment;
		}

		private void AfterEvent(MethodCall methodCall, [NotNull] IAgentWrapperApi agentWrapperApi, [NotNull] String eventName, [NotNull] HttpApplication httpApplication)
		{
			if (httpApplication == null)
				throw new ArgumentNullException("httpApplication");

			var httpContext = httpApplication.Context;
			if (httpContext == null)
				throw new NullReferenceException("httpApplication.Context");

			var segment = agentWrapperApi.CastAsSegment(httpContext.Items[HttpContextActions.HttpContextSegmentKey]);
			httpContext.Items[HttpContextActions.HttpContextSegmentKey] = null;
			segment.End();

			if (eventName == "EndRequest")
			{
				HttpContextActions.TransactionShutdown(agentWrapperApi, httpContext);
				agentWrapperApi.CurrentTransaction.End();
			}
		}

		[NotNull]
		private Object GetBeforeExecutionStep(MethodCall methodCall, [NotNull] IAgentWrapperApi agentWrapperApi, [NotNull] String eventName, [NotNull] HttpApplication httpApplication)
		{
			// ReSharper disable once AssignNullToNotNullAttribute
			EventHandler beforePipelineEventHandler = (sender, args) => BeforeEvent(methodCall, agentWrapperApi, eventName, sender as HttpApplication);
			beforePipelineEventHandler = GetExceptionSafeEventHandler(beforePipelineEventHandler, agentWrapperApi);

			var beforePipelineEventExecutionStep = Statics.CreateSyncEventExecutionStep(httpApplication, beforePipelineEventHandler);
			if (beforePipelineEventExecutionStep == null)
				throw new NullReferenceException("beforePipelineEventExecutionStep");

			return beforePipelineEventExecutionStep;
		}

		[NotNull]
		private Object GetAfterExecutionStep(MethodCall methodCall, [NotNull] IAgentWrapperApi agentWrapperApi, [NotNull] String eventName, [NotNull] HttpApplication httpApplication)
		{
			// ReSharper disable once AssignNullToNotNullAttribute
			EventHandler afterPipelineEventHandler = (sender, args) => AfterEvent(methodCall, agentWrapperApi, eventName, sender as HttpApplication);
			afterPipelineEventHandler = GetExceptionSafeEventHandler(afterPipelineEventHandler, agentWrapperApi);

			var afterPipelineEventExecptionStep = Statics.CreateSyncEventExecutionStep(httpApplication, afterPipelineEventHandler);
			if (afterPipelineEventExecptionStep == null)
				throw new NullReferenceException("afterPipelineEventExecptionStep");

			return afterPipelineEventExecptionStep;
		}

		[NotNull, Pure]
		private static EventHandler GetExceptionSafeEventHandler([NotNull] EventHandler eventHandler, [NotNull] IAgentWrapperApi agentWrapperApi)
		{
			return (sender, args) =>
			{
				agentWrapperApi.HandleExceptions(() => eventHandler(sender, args));
			};
		}
	}
}
