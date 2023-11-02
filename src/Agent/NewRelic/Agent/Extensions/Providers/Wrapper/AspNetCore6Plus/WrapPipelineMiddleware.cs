// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AspNetCore6Plus
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
        private volatile bool _inspectingHttpContextForErrorsIsEnabled = true;

        public WrapPipelineMiddleware(RequestDelegate next, IAgent agent)
        {
            _next = next;
            _agent = agent;
        }

        public async Task Invoke(HttpContext context)
        {
            ITransaction transaction = null;
            ISegment segment = null;

            // Don't create a transaction in this case to avoid MGIs associated with CORS pre-flight requests
            if ("OPTIONS".Equals(context.Request?.Method, StringComparison.OrdinalIgnoreCase))
            {
                _agent.Logger.Log(Agent.Extensions.Logging.Level.Finest, "Not instrumenting incoming OPTIONS request.");

                await _next(context);
                return;
            }

            try
            {
                transaction = SetupTransaction(context.Request);
                transaction.AttachToAsync(); //Important that this is called from an Invoke method that has the async keyword.
                transaction.DetachFromPrimary(); //Remove from thread-local type storage

                segment = SetupSegment(transaction, context);
                segment.AlwaysDeductChildDuration = true;

                if (_agent.Configuration.AllowAllRequestHeaders)
                {
                    transaction.SetRequestHeaders(context.Request.Headers, context.Request.Headers.Keys, GetHeaderValue);
                }
                else
                {
                    transaction.SetRequestHeaders(context.Request.Headers, Agent.Extensions.Providers.Wrapper.Statics.DefaultCaptureHeaders, GetHeaderValue);
                }

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

        private string GetHeaderValue(IHeaderDictionary headers, string key)
        {
            return headers[key];
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
                else if (_inspectingHttpContextForErrorsIsEnabled)
                {
                    try
                    {
                        Statics.NoticeErrorFromContextIfAvailable(context, transaction);
                    }
                    catch (Exception e)
                    {
                        //We need to catch and handle exceptions here so that the transaction and segment can still end appropriately

                        _inspectingHttpContextForErrorsIsEnabled = false;

                        _agent.Logger.Log(Agent.Extensions.Logging.Level.Info, "Inspecting errors from the IExceptionHandlerFeature is disabled, usually because that AspNetCore feature is not available.  Debug Level logs will contain more information.");
                        _agent.Logger.Log(Agent.Extensions.Logging.Level.Debug, $"Error when requesting IExceptionHandlerFeature: {e}");
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
            var methodCall = new MethodCall(method, this, new object[] { context }, true);

            var segment = transaction.StartTransactionSegment(methodCall, "Middleware Pipeline");
            return segment;
        }

        private ITransaction SetupTransaction(HttpRequest request)
        {
            var path = request.Path.Value;

            // if path is empty, consider it the same as /
            path = request.Path == PathString.Empty || path.Equals("/") ? "ROOT" : path.Substring(1);

            var transaction = _agent.CreateTransaction(
                    isWeb: true,
                    category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                    transactionDisplayName: path,
                    doNotTrackAsUnitOfWork: true);

            transaction.SetRequestMethod(request.Method);
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
            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(httpContext, GetHeaderValue, TransportType.HTTP);

            IEnumerable<string> GetHeaderValue(HttpContext context, string key)
            {
                string value = null;
                if (key.Equals("Content-Length"))
                {
                    value = context.Request.ContentLength.ToString();
                }
                else
                {
                    value = context.Request.Headers[key];
                }

                return value == null ? null : new string[] { value };
            }
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

        private static class Statics
        {
            public static void NoticeErrorFromContextIfAvailable(HttpContext context, ITransaction transaction)
            {
                //The IExceptionHandlerFeature type is not always available in an AspNetCore app. We need to guard its access
                //here to prevent the middleware from crashing when this type needs to be resolved. The type needs to be
                //fully qualified so that the middleware class does not get an error because of the using statement for
                //Microsoft.AspNetCore.Diagnostics.
                var exceptionHandlerFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                if (exceptionHandlerFeature != null)
                {
                    transaction.NoticeError(exceptionHandlerFeature.Error);
                }
            }
        }
    }
}
