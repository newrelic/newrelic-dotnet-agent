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

            transaction.SetWebTransactionName(WebTransactionType.ASP, transactionName, TransactionNamePriority.Uri); // TODO: What priority is correct here?

            // TODO: This probably isn't right - copied from `OtherTransactionWrapper`
            // for minimal api, Invoke() is going to eventually invoke the delegate that is mapped to this route. Not sure a segment is helpful here?
            var typeName = instrumentedMethodCall.MethodCall.Method.Type.FullName ?? "<unknown>";
            var methodName = instrumentedMethodCall.MethodCall.Method.MethodName;
            var segment = !string.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName)
                ? transaction.StartCustomSegment(instrumentedMethodCall.MethodCall, instrumentedMethodCall.RequestedMetricName)
                : transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, typeName, methodName);

            return Delegates.GetAsyncDelegateFor<Task>(agent, segment);
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
