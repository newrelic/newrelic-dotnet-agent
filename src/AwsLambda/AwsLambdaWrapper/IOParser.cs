// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.Lambda.SQSEvents;
using Amazon.Lambda.SNSEvents;
using Amazon.Lambda.KinesisEvents;
using Amazon.Lambda.S3Events;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.Lambda.KinesisFirehoseEvents;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace NewRelic.OpenTracing.AmazonLambda
{
    internal class IOParser
    {
        private const string NEWRELIC_TRACE_HEADER = "newrelic";

        private static List<string> _headersToAccept = new List<string>()
        {
            "content-type",
            "content-length",
            "x-forwarded-port",
            "x-forwarded-proto",

        };

        /// <summary>
        /// Attempt to parse a status code from the response object, which could be present if the event source type was
        /// created from an Application Load Balancer or API Gateway.
        /// </summary>
        /// <typeparam name="TInput">Type of the request object.</typeparam>
        /// <param name="request">The inbound request object prior to being handled by the hunction.</param>
        /// <returns> IDictionary containing items to add as tags to an ISpan.</returns>
        public static IDictionary<string, string> ParseRequest<TInput>(TInput request)
        {
            if (request != null)
            {
                // Only try to check the TInput Names without trying to load
                // actual types so as to not take a dependency on actual types unless
                // its already loaded
                switch (request.GetType().ToString())
                {
                    case "Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest":
                        return ParseApiGateway(request);
                    case "Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest":
                        return ParseLoadBalancer(request);
                    case "Amazon.Lambda.SQSEvents.SQSEvent":
                        return ParseSQSRequest(request);
                    case "Amazon.Lambda.SNSEvents.SNSEvent":
                        return ParseSNSRequest(request);
                    case "Amazon.Lambda.KinesisEvents.KinesisEvent":
                        return ParseKinesisRequest(request);
                    case "Amazon.Lambda.DynamoDBEvents.DynamoDBEvent":
                        return ParseDynamoDBRequest(request);
                    case "Amazon.Lambda.KinesisFirehoseEvents.KinesisFirehoseEvent":
                        return ParseFirehoseRequest(request);
                    case "Amazon.Lambda.S3Events.S3Event":
                        return ParseS3Request(request);
                    default:
                        break;
                }
            }
            return new Dictionary<string, string>();

        }

        /// <summary>
        /// Parse API Gateway Requests
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IDictionary<string, string> ParseApiGateway(object request)
        {
            var tags = new Dictionary<string, string>();

            // APIGatewayProxyRequest puts heads in both Headers and MultiValueHeaders so read from MultiValueHeaders
            if (request is APIGatewayProxyRequest proxyRequest)
            {
                tags.Add("method", proxyRequest.HttpMethod);
                tags.Add("uri", proxyRequest.Path);
                ParseHeaders(proxyRequest.Headers, proxyRequest.MultiValueHeaders, tags);
            }

            return tags;
        }

        /// <summary>
        /// Parse LoadBalancer Requests
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IDictionary<string, string> ParseLoadBalancer(object request)
        {
            var tags = new Dictionary<string, string>();
            // ApplicationLoadBalancerRequest puts headers in ONLY in either Headers and MultiValueHeaders so we need to check
            if (request is ApplicationLoadBalancerRequest loadBalancerRequest)
            {
                TrySetInvocationSourceTag(loadBalancerRequest.RequestContext?.Elb?.TargetGroupArn, tags);
                tags.Add("method", loadBalancerRequest.HttpMethod);
                tags.Add("uri", loadBalancerRequest.Path);
                ParseHeaders(loadBalancerRequest.Headers, loadBalancerRequest.MultiValueHeaders, tags);
            }
            return tags;
        }


        /// <summary>
        /// Parse SQS Requests
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IDictionary<string, string> ParseSQSRequest(object request)
        {
            var tags = new Dictionary<string, string>();
            // Spec Compliance
            if (request is SQSEvent sqsRequest)
            {
                var record = sqsRequest.Records?[0];
                TrySetInvocationSourceTag(record?.EventSourceArn, tags);
                if (record?.MessageAttributes != null && record.MessageAttributes.ContainsKey(NEWRELIC_TRACE_HEADER))
                {
                    tags.Add(NEWRELIC_TRACE_HEADER, record.MessageAttributes[NEWRELIC_TRACE_HEADER].StringValue);
                }
                else if (record?.Body != null && record.Body.Contains("\"Type\" : \"Notification\"") && record.Body.Contains("\"MessageAttributes\""))
                {
                    // This is an an SNS subscription with atttributes
                    var newrelicIndex = record.Body.IndexOf("newrelic", System.StringComparison.InvariantCultureIgnoreCase) + 9;
                    var startIndex = record.Body.IndexOf("Value\":\"", newrelicIndex, System.StringComparison.InvariantCultureIgnoreCase) + 8;
                    var endIndex = record.Body.IndexOf('"', startIndex);
                    var payload = record.Body.Substring(startIndex, endIndex - startIndex);
                    tags.Add(NEWRELIC_TRACE_HEADER, payload);
                }
            }

            return tags;
        }

        /// <summary>
        /// Parse SNS Requests
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IDictionary<string, string> ParseSNSRequest(object request)
        {
            var tags = new Dictionary<string, string>();
            // Spec Compliance
            if (request is SNSEvent snsRequest)
            {
                var record = snsRequest.Records?[0];
                TrySetInvocationSourceTag(record?.EventSubscriptionArn, tags);
                if (record?.Sns?.MessageAttributes != null && record.Sns.MessageAttributes.ContainsKey(NEWRELIC_TRACE_HEADER))
                {
                    tags.Add(NEWRELIC_TRACE_HEADER, record.Sns.MessageAttributes[NEWRELIC_TRACE_HEADER].Value);
                }
            }

            return tags;
        }

        /// <summary>
        /// Parse Kinesis Request
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IDictionary<string, string> ParseKinesisRequest(object request)
        {
            var tags = new Dictionary<string, string>();
            // Spec Compliance
            if (request is KinesisEvent kinesisRequest)
            {
                TrySetInvocationSourceTag(kinesisRequest.Records?[0]?.EventSourceARN, tags);
            }
            return tags;
        }

        /// <summary>
        /// Parse S3 Requests
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IDictionary<string, string> ParseS3Request(object request)
        {
            var tags = new Dictionary<string, string>();
            if (request is S3Event s3Request)
            {
                TrySetInvocationSourceTag(s3Request.Records?[0]?.S3?.Bucket?.Arn, tags);
            }
            return tags;
        }

        /// <summary>
        /// Parse DynamoDB Request
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IDictionary<string, string> ParseDynamoDBRequest(object request)
        {
            var tags = new Dictionary<string, string>();
            // Spec Compliance
            if (request is DynamoDBEvent dynamoDBRequest)
            {
                TrySetInvocationSourceTag(dynamoDBRequest.Records?[0]?.EventSourceArn, tags);
            }
            return tags;
        }

        /// <summary>
        /// Parse Firehose Request
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IDictionary<string, string> ParseFirehoseRequest(object request)
        {
            var tags = new Dictionary<string, string>();
            // Spec Compliance
            if (request is KinesisFirehoseEvent kinesisFirehoseRequest)
            {
                TrySetInvocationSourceTag(kinesisFirehoseRequest.DeliveryStreamArn, tags);
            }
            return tags;
        }

        /// <summary>
        /// Attempt to parse a status code from the response object, which could be present if the event source type was
        /// created from an Application Load Balancer or API Gateway.
        /// </summary>
        /// <typeparam name="TOutput">Type of the response object.</typeparam>
        /// <param name="response">The response from function handler.</param>
        /// <returns>IDictionary containing items to add as tags to an ISpan.</returns>
        public static IDictionary<string, string> ParseResponse<TOutput>(TOutput response)
        {
            var tags = new Dictionary<string, string>();

            // Cover Stream, string, etc., eventuall

            if (response is IDictionary map)
            {
                if (map["statusCode"] is string statusCodeString)
                {
                    tags.Add("status", statusCodeString);
                    return tags;
                }

                var statusCodeInt = map["statusCode"] as int?;
                if (statusCodeInt != null)
                {
                    tags.Add("status", statusCodeInt.Value.ToString());
                    return tags;
                }

                return tags;
            }

            switch (response.GetType().ToString())
            {
                case "Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse":
                    return ParseGatewayResponse(response);
                case "Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerResponse":
                    return ParseLoadBalancerResponse(response);
                default:
                    break;
            }
            return tags;
        }


        /// <summary>
        /// Parse Gateway Response
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IDictionary<string, string> ParseGatewayResponse(object response)
        {
            var tags = new Dictionary<string, string>();
            // APIGatewayProxyResponse puts heads in both Headers and MultiValueHeaders so read from MultiValueHeaders
            if (response is APIGatewayProxyResponse proxyResponse)
            {
                tags.Add("status", proxyResponse.StatusCode.ToString());
                ParseHeaders(proxyResponse.Headers, proxyResponse.MultiValueHeaders, tags);
            }
            return tags;
        }

        /// <summary>
        /// Parse Load Balancer Response
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IDictionary<string, string> ParseLoadBalancerResponse(object response)
        {
            var tags = new Dictionary<string, string>();
            // ApplicationLoadBalancerRequest puts headers in ONLY in either Headers and MultiValueHeaders so we need to check
            if (response is ApplicationLoadBalancerResponse loadBalancerResponse)
            {
                tags.Add("status", loadBalancerResponse.StatusCode.ToString());
                ParseHeaders(loadBalancerResponse.Headers, loadBalancerResponse.MultiValueHeaders, tags);
            }
            return tags;
        }


        #region Helpers

        /// <summary>
        /// Parses out the headers from a request or response
        /// </summary>
        /// <param name="singleValueHeaders">The headers single value collection.</param>
        /// <param name="multiValueHeaders">The headers multi-value collection.</param>
        /// <param name="tags">The tags dictionary</param>
        private static void ParseHeaders(IDictionary<string, string> singleValueHeaders, IDictionary<string, IList<string>> multiValueHeaders, IDictionary<string, string> tags)
        {
            if (multiValueHeaders != null)
            {
                foreach (var header in multiValueHeaders)
                {
                    var headerName = header.Key.ToLower();
                    if (_headersToAccept.Contains(headerName))
                    {
                        tags.Add($"headers.{headerName}", string.Join(",", header.Value));
                    }
                    else if (headerName == NEWRELIC_TRACE_HEADER)
                    {
                        tags.Add(NEWRELIC_TRACE_HEADER, string.Join(",", header.Value));
                    }
                }
            }
            // do single value last since the docs indicate it might still be populated even if multi-value is used, just with the last value.
            else if (singleValueHeaders != null)
            {
                foreach (var header in singleValueHeaders)
                {
                    var headerName = header.Key.ToLower();
                    if (_headersToAccept.Contains(headerName))
                    {
                        tags.Add($"headers.{headerName}", (string)header.Value);
                    }
                    else if (headerName == NEWRELIC_TRACE_HEADER)
                    {
                        tags.Add(NEWRELIC_TRACE_HEADER, (string)header.Value);
                    }
                }
            }
        }

        private static void TrySetInvocationSourceTag(string arn, Dictionary<string, string> tags)
        {
            if (!string.IsNullOrEmpty(arn) && tags != null)
            {
                tags.Add("aws.lambda.eventSource.arn", arn);
            }
        }

        #endregion
    }
}

