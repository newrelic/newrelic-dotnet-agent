/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using Amazon.Lambda.Core;
using NewRelic.OpenTracing.AmazonLambda.DiagnosticObserver;
using NewRelic.OpenTracing.AmazonLambda.Helpers;
using OpenTracing;
using OpenTracing.Propagation;
using OpenTracing.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace NewRelic.OpenTracing.AmazonLambda
{
    public class TracingRequestHandler
    {
        private static int _isColdStart = 1;
        private static ITracer _tracer;
        private static IDisposable _listenerSubscription = DiagnosticListener.AllListeners.Subscribe(new CoreFxDiagnosticObserver());

        static TracingRequestHandler()
        {
            _tracer = GlobalTracer.Instance;
        }

        #region Wrappers

        /// <summary>
        /// Handles return types of Task and Task<>
        /// For task it calls a Wait() and for Task<> it calls Result() to ensure
        /// the task is completed for accurate timing and parsing output
        /// </summary>
        /// <param name="output"></param>
        /// <returns></returns>
        private object HandleTaskReturn(object output)
        {
            object realOutput = output;
            if (output != null)
            {
                // if output is not null determine whether the output
                // is of type Task. Task is converted to Task<VoidTaskResult>
                // so that is covered too. 
                Type outputType = output.GetType();
                if (outputType.IsGenericType &&
                    output is Task)
                {
                    try
                    {
                        // If the return type is Task (which is always Task<T> retrieve the result for it
                        // that should wait for the task to complete as well. 
                        realOutput = outputType.GetProperty("Result").GetValue(output);
                    }
                    catch (TargetInvocationException ex)
                    {
                        // Since we are using reflection here, the exception thrown from within the 
                        // handler is wrapped with TargetInvocationException, so we want to catch it and
                        // throw the innerexception is available
                        if (ex.InnerException != null)
                        {
                            ExceptionDispatchInfo info = null;
                            if (ex.InnerException is AggregateException && ex.InnerException.InnerException != null)
                                info = ExceptionDispatchInfo.Capture(ex.InnerException.InnerException);
                            else
                                info = ExceptionDispatchInfo.Capture(ex.InnerException);
                            info.Throw();
                        }
                        else
                            throw;
                    }

                }
            }
            return realOutput;
        }

        public TOut LambdaWrapper<TInput, TOut>(Func<TInput, ILambdaContext, TOut> handler, TInput input, ILambdaContext context, ISpanContext distributedTraceContext = null)
        {
            var scope = BeforeWrappedMethod(handler.Method.Name, context, distributedTraceContext, input);
            TOut output = default(TOut);
            object realOutput = null;
            try
            {
                output = handler(input, context);
                // Handle Tasks
                realOutput = HandleTaskReturn(output);
            }
            catch (Exception ex)
            {
                AfterWrappedMethod(scope: scope, handlerException: ex);
                throw;
            }

            AfterWrappedMethod(scope: scope, output: realOutput);
            return output;
        }

        public TOut LambdaWrapper<TOut>(Func<ILambdaContext, TOut> handler, ILambdaContext context, ISpanContext distributedTraceContext = null)
        {
            var scope = BeforeWrappedMethod(handler.Method.Name, context, distributedTraceContext);
            TOut output = default(TOut);
            object realOutput = null;

            try
            {
                output = handler(context);
                // Handle Tasks
                realOutput = HandleTaskReturn(output);
            }
            catch (Exception ex)
            {
                AfterWrappedMethod(scope: scope, handlerException: ex);
                throw;
            }

            AfterWrappedMethod(scope: scope, output: realOutput);
            return output;
        }

        public void LambdaWrapper<TInput>(Action<TInput, ILambdaContext> handler, TInput input, ILambdaContext context, ISpanContext distributedTraceContext = null)
        {
            var scope = BeforeWrappedMethod(handler.Method.Name, context, distributedTraceContext, input);

            try
            {
                handler(input, context);
            }
            catch (Exception ex)
            {
                AfterWrappedMethod(scope: scope, handlerException: ex);
                throw;
            }

            AfterWrappedMethod(scope: scope);
        }

        public void LambdaWrapper(Action<ILambdaContext> handler, ILambdaContext context, ISpanContext distributedTraceContext = null)
        {
            var scope = BeforeWrappedMethod(handler.Method.Name, context, distributedTraceContext);

            try
            {
                handler(context);
            }
            catch (Exception ex)
            {
                AfterWrappedMethod(scope: scope, handlerException: ex);
                throw;
            }

            AfterWrappedMethod(scope: scope);
        }

        #endregion

        private IScope BeforeWrappedMethod(string name, ILambdaContext lambdaContext, ISpanContext distributedTraceContext, object input = null)
        {
            // Should we use  _tracer.ScopeManager.Active instead?  It returns null if none is active;
            IScope scope = null;

            try
            {
                var tags = IOParser.ParseRequest(input);
                var extractAdapter = new TextMapExtractAdapter(tags);
                var spanContext = distributedTraceContext ?? _tracer.Extract(BuiltinFormats.TextMap, extractAdapter);
                tags.Remove("newrelic");
                scope = _tracer
                    .BuildSpan(name)
                    .AsChildOf(spanContext)
                    .WithTag("aws.requestId", lambdaContext.AwsRequestId ?? string.Empty)
                    .WithTag("aws.arn", lambdaContext.InvokedFunctionArn ?? string.Empty)
                    .StartActive();

                DetectColdStart(scope, ref _isColdStart);

                if (input != null)
                {
                    AddTagsToActiveSpan(scope.Span, "request", tags);
                }
            }
            catch (Exception exception)
            {
                Logger.Log(message: exception.ToString(), level: "ERROR");
            }

            return scope;
        }

        private void AfterWrappedMethod(IScope scope, object output = null, Exception handlerException = null)
        {
            try
            {
                if (output != null && scope != null)
                {
                    var tags = IOParser.ParseResponse(output);
                    tags.Remove("newrelic");
                    AddTagsToActiveSpan(scope.Span, "response", tags);
                }

                if (handlerException != null && scope != null)
                {
                    scope.Span.SetException(handlerException);
                }
            }
            catch (Exception exception)
            {
                Logger.Log(message: exception.ToString(), level: "ERROR");
            }
            if (scope != null)
            {
                scope.Dispose();
            }
        }

        internal static void DetectColdStart(IScope scope, ref int isColdStart)
        {
            if (Interlocked.CompareExchange(ref isColdStart, 0, 1) == 1)
            {
                scope.Span.SetTag("aws.lambda.coldStart", true);
            }
        }

        internal static void AddTagsToActiveSpan(ISpan span, string prefix, IDictionary<string, string> tags)
        {
            if (span == null || tags == null || string.IsNullOrEmpty(prefix))
            {
                return;
            }

            foreach (var tag in tags)
            {
                // If tag starts with aws treat it special so as to not add the prefix
                // Prefix is particularly required for http request / response style tags. 
                if (tag.Key.StartsWith("aws"))
                    span.SetTag(tag.Key, tag.Value);
                else
                    span.SetTag($"{prefix}.{tag.Key}", tag.Value);
            }
        }
    }
}
