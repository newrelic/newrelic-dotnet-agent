// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Providers.Wrapper.Asp35.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NewRelic.Providers.Wrapper.Asp35.IntegratedPipeline
{
    public class ExecuteStepWrapper : IWrapper
    {
        public bool IsTransactionRequired => false;

        public const string WrapperName = "Asp35.ExecuteStepTracer";

        public static class Statics
        {
            #region private static readonly IEnumerable<string> PossibleEvents

            public static readonly IEnumerable<string> PossibleEvents = new List<string>
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
            public static IDictionary<RequestNotification, string> RequestNotificationToStringMap
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
            private static IDictionary<RequestNotification, string> _requestNotificationToString;
        }

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var canWrap = methodInfo.RequestedWrapperName.Equals(WrapperName, StringComparison.OrdinalIgnoreCase);
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
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
            var lastRequestNotification = httpContext.Items[HttpContextActions.HttpContextSegmentTypeKey] as string;
            if (requestNotification == lastRequestNotification)
                return Delegates.NoOp;

            // if there is no transaction or segment yet then this will do nothing
            var segment = agent.CastAsSegment(httpContext.Items[HttpContextActions.HttpContextSegmentKey]);
            httpContext.Items[HttpContextActions.HttpContextSegmentKey] = null;
            httpContext.Items[HttpContextActions.HttpContextSegmentTypeKey] = null;
            segment.End();

            transaction = TryCreateTransaction(agent, httpContext, requestNotification);
            segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, requestNotification);
            segment.AlwaysDeductChildDuration = true;

            httpContext.Items[HttpContextActions.HttpContextSegmentKey] = segment;
            httpContext.Items[HttpContextActions.HttpContextSegmentTypeKey] = requestNotification;

            return Delegates.NoOp;
        }

        private ITransaction TryCreateTransaction(IAgent agent, HttpContext httpContext, string requestNotification)
        {
            // MapRequestHandler is always called so if we make it past that without having already started a transaction then don't start one since we already missed too much.  This is likely to occur during startup when the transaction service spins up half way through a request.
            var earlyEnoughInTransactionLifecycleToCreate = Statics.PossibleEvents
                .TakeWhile(@event => @event != "AcquireRequestState")
                .Where(@event => @event == requestNotification)
                .Any();
            if (!earlyEnoughInTransactionLifecycleToCreate)
                return agent.CurrentTransaction;

            Action onCreate = () =>
            {
                HttpContextActions.TransactionStartup(agent, httpContext);
            };

            return agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "Integrated Pipeline",
                doNotTrackAsUnitOfWork: true,
                wrapperOnCreate: onCreate);
        }
    }
}
