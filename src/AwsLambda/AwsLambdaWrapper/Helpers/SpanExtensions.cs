/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using OpenTracing;
using OpenTracing.Propagation;
using OpenTracing.Tag;
using OpenTracing.Util;
using System;
using System.Collections.Generic;

namespace NewRelic.OpenTracing.AmazonLambda.Helpers
{
    internal static class SpanExtensions
    {
        public static void SetException(this ISpan span, Exception e)
        {
            if (span == null || e == null)
                return;

            span.Log(CreateSpanErrorAttributes(e));

        }

        /// <summary>
        /// Add Error Attributes to the span after extracting from Exception
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        private static IDictionary<string, object> CreateSpanErrorAttributes(Exception exception)
        {
            IDictionary<string, object> errorAttributes = new Dictionary<string, object>();
            errorAttributes.Add("event", Tags.Error.Key);
            errorAttributes.Add("error.object", exception);
            errorAttributes.Add("message", exception.Message);
            errorAttributes.Add("stack", exception.StackTrace);
            errorAttributes.Add("error.kind", "Exception");
            return errorAttributes;
        }

        internal static void ApplyDistributedTracePayload(this ISpan span, Dictionary<string, Amazon.SQS.Model.MessageAttributeValue> messageAttributes)
        {
            if (span == null || messageAttributes == null)
            {
                return;
            }

            var injector = new SQSInjectAdapter(messageAttributes);
            GlobalTracer.Instance.Inject(span.Context, BuiltinFormats.HttpHeaders, injector); // Using BuiltinFormats.HttpHeaders since we want the "safe" string.
        }

        internal static void ApplyDistributedTracePayload(this ISpan span, Dictionary<string, Amazon.SimpleNotificationService.Model.MessageAttributeValue> messageAttributes)
        {
            if (span == null || messageAttributes == null)
            {
                return;
            }

            var injector = new SNSInjectAdapter(messageAttributes);
            GlobalTracer.Instance.Inject(span.Context, BuiltinFormats.HttpHeaders, injector); // Using BuiltinFormats.HttpHeaders since we want the "safe" string.
        }
    }
}
