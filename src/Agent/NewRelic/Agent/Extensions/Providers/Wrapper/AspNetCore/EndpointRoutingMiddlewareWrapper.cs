// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;

namespace NewRelic.Providers.Wrapper.AspNetCore
{
    public class EndpointRoutingMiddlewareWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse("NewRelic.Providers.Wrapper.AspNetCore.EndpointRouting.Invoke".Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall,
            IAgent agent, ITransaction transaction)
        {
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            var request = ((HttpContext)instrumentedMethodCall.MethodCall.MethodArguments[0]).Request;

            var transactionName = CreateTransactionName(request);

            agent.Logger.Log(Agent.Extensions.Logging.Level.Debug,$"EndpointRoutingMiddlewareWrapper set transaction name to {transactionName}");

            transaction.SetWebTransactionName(WebTransactionType.ASP, transactionName, TransactionNamePriority.FrameworkHigh);

            //var controllerTypeInfo = controllerContext.ActionDescriptor.ControllerTypeInfo;
            ////Framework uses ControllerType.Action for these metrics & transactions. WebApi is Controller.Action for both
            ////Taking opinionated stance to do ControllerType.MethodName for segments. Controller/Action for transactions
            //var controllerTypeName = controllerTypeInfo.Name;
            //var methodName = controllerContext.ActionDescriptor.MethodInfo.Name;

            // TODO: figure out what to do here - Minimal APIs don't have a controller or method name to pull in
            var segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, "controllerTypeName", "methodName");

            var segmentApi = segment.GetExperimentalApi();
            segmentApi.UserCodeNamespace = "controllerTypeInfo.FullName";
            segmentApi.UserCodeFunction = "methodName";

            return Delegates.GetAsyncDelegateFor<Task>(agent, segment, TaskContinueWithOption.None);
        }

        private static string CreateTransactionName(HttpRequest request)
        {

            var path = request.Path;
            var method = request.Method.ToLower().CapitalizeWord(); // ensure it looks like "Post" rather than "POST"

            var transactionName = $"{path}/{method}";
            transactionName = transactionName.TrimStart('/');

            foreach (var parameter in request.Query)
            {
                transactionName += "/{" + parameter.Key + "}";
            }

            return transactionName;
        }
    }
}
