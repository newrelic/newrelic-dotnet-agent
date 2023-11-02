// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using Microsoft.Owin;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.Owin
{
    internal class OwinStartupMiddleware : OwinMiddleware
    {
        private readonly OwinMiddleware _next;
        private readonly IAgent _agent;

        public OwinStartupMiddleware(OwinMiddleware next, IAgent agent)
            : base(next)
        {
            _next = next;
            _agent = agent;
        }

        public override async Task Invoke(IOwinContext context)
        {
            ISegment segment = null;
            ITransaction transaction = null;

            try
            {
                transaction = SetupTransaction(context.Request);
                transaction.AttachToAsync();
                transaction.DetachFromPrimary();

                segment = SetupSegment(transaction, context);
                segment.AlwaysDeductChildDuration = true;

                if (_agent.Configuration.AllowAllRequestHeaders)
                {
                    transaction.SetRequestHeaders(context.Request.Headers, context.Request.Headers.Keys, GetHeaderValue);
                }
                else
                {
                    transaction.SetRequestHeaders(context.Request.Headers, Statics.DefaultCaptureHeaders, GetHeaderValue);
                }

                ProcessHeaders(context);

                context.Response.OnSendingHeaders(SetOutboundTracingDataAsync, null);
            }
            catch (Exception ex)
            {
                _agent.SafeHandleException(ex);
            }

            try
            {
                await _next.Invoke(context);
                EndTransaction(segment, transaction, context, null);
            }
            catch (Exception ex)
            {
                EndTransaction(segment, transaction, context, ex);
                throw; //throw here to maintain call stack. 
            }

            void SetOutboundTracingDataAsync(object state)
            {
                TryWriteResponseHeaders(context, transaction);
            }
        }

        private ITransaction SetupTransaction(IOwinRequest request)
        {
            var path = request.Path.Value;
            path = "/".Equals(path) ? "ROOT" : path.Substring(1);

            var transaction = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Custom),
                transactionDisplayName: path,
                doNotTrackAsUnitOfWork: true);

            transaction.SetRequestMethod(request.Method);
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
            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(owinContext, GetHeaderValue, TransportType.HTTP);
        }

        private IEnumerable<string> GetHeaderValue(IOwinContext owinContext, string key)
        {
            var value = owinContext.Request.Headers[key];
            return value == null ? null : new string[] { value };
        }

        private ISegment SetupSegment(ITransaction transaction, IOwinContext owinContext)
        {
            var method = new Method(typeof(OwinStartupMiddleware), nameof(Invoke), nameof(owinContext));
            var methodCall = new MethodCall(method, this, new object[] { owinContext }, true);

            var segment = transaction.StartTransactionSegment(methodCall, "Owin Middleware Pipeline");
            return segment;
        }

        private string GetHeaderValue(IHeaderDictionary headers, string key)
        {
            return headers[key];
        }


        private void EndTransaction(ISegment segment, ITransaction transaction, IOwinContext owinContext, Exception appException)
        {
            try
            {
                var responseStatusCode = owinContext.Response.StatusCode;

                if (appException != null)
                {
                    transaction.NoticeError(appException);

                    //Response code may not be 500 for exception cases,
                    //because that appears to be handled at the  web host or server level
                    responseStatusCode = 500;
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

        private void TryWriteResponseHeaders(IOwinContext owinContext, ITransaction transaction)
        {
            try
            {
                var headers = transaction.GetResponseMetadata();

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
                _agent.SafeHandleException(ex);
            }
        }
    }
}
