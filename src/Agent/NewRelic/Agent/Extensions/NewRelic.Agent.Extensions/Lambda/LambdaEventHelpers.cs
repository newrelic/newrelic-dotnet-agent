// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.JsonConverters;
using NewRelic.Core.JsonConverters.LambdaPayloads;

namespace NewRelic.Agent.Extensions.Lambda;

public static class LambdaEventHelpers
{
    private static int _sqsTracingHeadersParsingMaxExceptionsToLog = 3;

    public static void AddEventTypeAttributes(IAgent agent, ITransaction transaction, AwsLambdaEventType eventType, object inputObject)
    {
        try
        {
            switch (eventType)
            {
                case AwsLambdaEventType.APIGatewayProxyRequest:
                    dynamic apiReqEvent = inputObject; // Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest
                    SetWebRequestProperties(agent, transaction, apiReqEvent, eventType);

                    if (apiReqEvent.RequestContext != null)
                    {
                        dynamic requestContext = apiReqEvent.RequestContext;
                        // arn is not available
                        transaction.AddEventSourceAttribute("accountId", (string)requestContext.AccountId);
                        transaction.AddEventSourceAttribute("apiId", (string)requestContext.ApiId);
                        transaction.AddEventSourceAttribute("resourceId", (string)requestContext.ResourceId);
                        transaction.AddEventSourceAttribute("resourcePath", (string)requestContext.ResourcePath);
                        transaction.AddEventSourceAttribute("stage", (string)requestContext.Stage);
                    }
                    break;

                case AwsLambdaEventType.APIGatewayHttpApiV2ProxyRequest:
                    dynamic apiReqv2Event = inputObject; // Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest
                    SetWebRequestProperties(agent, transaction, apiReqv2Event, eventType);

                    if (apiReqv2Event.RequestContext != null)
                    {
                        dynamic requestContext = apiReqv2Event.RequestContext;
                        // arn is not available
                        transaction.AddEventSourceAttribute("accountId", (string)requestContext.AccountId);
                        transaction.AddEventSourceAttribute("apiId", (string)requestContext.ApiId);
                        // resourceId is not available for v2
                        // resourcePath is not available for v2
                        transaction.AddEventSourceAttribute("stage", (string)requestContext.Stage);
                    }
                    break;

                case AwsLambdaEventType.ApplicationLoadBalancerRequest:
                    dynamic albReqEvent = inputObject; //Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest

                    SetWebRequestProperties(agent, transaction, albReqEvent, eventType);

                    transaction.AddEventSourceAttribute("arn", (string)albReqEvent.RequestContext.Elb.TargetGroupArn);
                    break;

                case AwsLambdaEventType.CloudWatchScheduledEvent:
                    dynamic cloudWatchScheduledEvent = inputObject; //Amazon.Lambda.CloudWatchEvents.ScheduledEvents.ScheduledEvent

                    if (cloudWatchScheduledEvent.Resources != null && cloudWatchScheduledEvent.Resources.Count > 0)
                    {
                        transaction.AddEventSourceAttribute("arn", (string)cloudWatchScheduledEvent.Resources[0]);
                        transaction.AddEventSourceAttribute("account", (string)cloudWatchScheduledEvent.Account);
                        transaction.AddEventSourceAttribute("id", (string)cloudWatchScheduledEvent.Id);
                        transaction.AddEventSourceAttribute("region", (string)cloudWatchScheduledEvent.Region);
                        transaction.AddEventSourceAttribute("resource", (string)cloudWatchScheduledEvent.Resources[0]);
                        transaction.AddEventSourceAttribute("time", ((DateTime)cloudWatchScheduledEvent.Time).ToString());
                    }
                    break;

                case AwsLambdaEventType.KinesisStreamingEvent:
                    dynamic kinesisStreamingEvent = inputObject; //Amazon.Lambda.KinesisEvents.KinesisEvent

                    if (kinesisStreamingEvent.Records != null && kinesisStreamingEvent.Records.Count > 0)
                    {
                        transaction.AddEventSourceAttribute("arn", (string)kinesisStreamingEvent.Records[0].EventSourceARN);
                        transaction.AddEventSourceAttribute("length", (int)kinesisStreamingEvent.Records.Count);
                        transaction.AddEventSourceAttribute("region", (string)kinesisStreamingEvent.Records[0].AwsRegion);
                    }
                    break;

                case AwsLambdaEventType.KinesisFirehoseEvent:
                    dynamic kinesisFirehoseEvent = inputObject; //Amazon.Lambda.KinesisFirehoseEvents.KinesisFirehoseEvent

                    if (kinesisFirehoseEvent.Records != null)
                    {
                        transaction.AddEventSourceAttribute("arn", (string)kinesisFirehoseEvent.DeliveryStreamArn);
                        transaction.AddEventSourceAttribute("length", (int)kinesisFirehoseEvent.Records.Count);
                        transaction.AddEventSourceAttribute("region", (string)kinesisFirehoseEvent.Region);
                    }
                    break;

                case AwsLambdaEventType.S3Event:
                    dynamic s3Event = inputObject; //Amazon.Lambda.S3Events.S3Event

                    if (s3Event.Records != null && s3Event.Records.Count > 0)
                    {
                        transaction.AddEventSourceAttribute("arn", (string)s3Event.Records[0].S3.Bucket.Arn);
                        transaction.AddEventSourceAttribute("length", (int)s3Event.Records.Count);
                        transaction.AddEventSourceAttribute("region", (string)s3Event.Records[0].AwsRegion);
                        transaction.AddEventSourceAttribute("eventName", (string)s3Event.Records[0].EventName);
                        transaction.AddEventSourceAttribute("eventTime", ((DateTime)s3Event.Records[0].EventTime).ToString());
                        transaction.AddEventSourceAttribute("xAmzId2", (string)s3Event.Records[0].ResponseElements.XAmzId2);
                        transaction.AddEventSourceAttribute("bucketName", (string)s3Event.Records[0].S3.Bucket.Name);
                        transaction.AddEventSourceAttribute("objectKey", (string)s3Event.Records[0].S3.Object.Key);
                        transaction.AddEventSourceAttribute("objectSequencer", (string)s3Event.Records[0].S3.Object.Sequencer);
                        transaction.AddEventSourceAttribute("objectSize", (long)s3Event.Records[0].S3.Object.Size);
                    }
                    break;

                case AwsLambdaEventType.SimpleEmailEvent:
                    dynamic sesEvent = inputObject; //Amazon.Lambda.SimpleEmailEvents.SimpleEmailEvent

                    if (sesEvent.Records != null && sesEvent.Records.Count > 0)
                    {
                        // arn is not available
                        transaction.AddEventSourceAttribute("length", (int)sesEvent.Records.Count);
                        transaction.AddEventSourceAttribute("date", (string)sesEvent.Records[0].Ses.Mail.CommonHeaders.Date);
                        transaction.AddEventSourceAttribute("messageId", (string)sesEvent.Records[0].Ses.Mail.CommonHeaders.MessageId);
                        transaction.AddEventSourceAttribute("returnPath", (string)sesEvent.Records[0].Ses.Mail.CommonHeaders.ReturnPath);
                    }
                    break;

                case AwsLambdaEventType.SNSEvent:
                    dynamic snsEvent = inputObject; //Amazon.Lambda.SNSEvents.SNSEvent

                    if (snsEvent.Records != null && snsEvent.Records.Count > 0)
                    {
                        transaction.AddEventSourceAttribute("arn", (string)snsEvent.Records[0].EventSubscriptionArn);
                        transaction.AddEventSourceAttribute("length", (int)snsEvent.Records.Count);
                        transaction.AddEventSourceAttribute("messageId", (string)snsEvent.Records[0].Sns.MessageId);
                        transaction.AddEventSourceAttribute("timestamp", ((DateTime)snsEvent.Records[0].Sns.Timestamp).ToString());
                        transaction.AddEventSourceAttribute("topicArn", (string)snsEvent.Records[0].Sns.TopicArn);
                        transaction.AddEventSourceAttribute("type", (string)snsEvent.Records[0].Sns.Type);

                        TryParseSNSDistributedTraceHeaders(snsEvent, transaction);
                    }
                    break;

                case AwsLambdaEventType.SQSEvent:
                    dynamic sqsEvent = inputObject; //Amazon.Lambda.SQSEvents.SQSEvent

                    if (sqsEvent.Records != null && sqsEvent.Records.Count > 0)
                    {
                        transaction.AddEventSourceAttribute("arn", (string)sqsEvent.Records[0].EventSourceArn);
                        transaction.AddEventSourceAttribute("length", (int)sqsEvent.Records.Count);
                        transaction.AddEventSourceAttribute("messageId", (string)sqsEvent.Records[0].MessageId);

                        TryParseSQSDistributedTraceHeaders(sqsEvent, transaction, agent);
                    }
                    break;

                case AwsLambdaEventType.DynamoStream:
                    dynamic dynamoEvent = inputObject; //Amazon.Lambda.DynamoDBEvents.DynamoDBEvent is the expected class or base class

                    if (dynamoEvent.Records != null && dynamoEvent.Records.Count > 0)
                    {
                        transaction.AddEventSourceAttribute("arn", (string)dynamoEvent.Records[0].EventSourceArn);
                    }
                    break;

                case AwsLambdaEventType.Unknown:
                    break; // nothing to do for unknown event type

                default:
                    throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Unexpected eventType");
            }
        }
        catch (Exception e)
        {
            agent.Logger.Log(Logging.Level.Warn, $"Unexpected exception in AddEventTypeAttributes(). Event type {eventType} had an inputObject of type {inputObject.GetType().FullName}. Exception: {e}");
            throw;
        }
    }

    private const string xForwardedProtoHeader = "X-Forwarded-Proto";
    private const string NEWRELIC_TRACE_HEADER = "newrelic";
    private const string W3C_TRACEPARENT_HEADER = "traceparent";
    private const string W3C_TRACESTATE_HEADER = "tracestate";
    private static readonly List<string> TracingKeys = new List<string> { NEWRELIC_TRACE_HEADER, W3C_TRACEPARENT_HEADER, W3C_TRACESTATE_HEADER };
    private const string SQS_MSG_ATTR_VALUE_PREFIX = @"Value"":""";

    private static void TryParseSQSDistributedTraceHeaders(dynamic sqsEvent, ITransaction transaction, IAgent agent)
    {
        // We can't pass anything dynamic to AcceptDTHeaders, so we have to copy the sqs
        // message attributes to a new <string,string> dict and then pass that to AcceptDTHeaders
        IDictionary<string, string> sqsHeaders = new Dictionary<string, string>();

        var record = sqsEvent.Records[0];
        if (record.MessageAttributes != null)
        {
            foreach (var tracingKey in TracingKeys)
            {
                if (record.MessageAttributes.ContainsKey(tracingKey))
                {
                    sqsHeaders.Add(tracingKey, record.MessageAttributes[tracingKey].StringValue);
                }
            }
        }
        else if (record.Body != null && record.Body.Contains("\"Notification\"") && record.Body.Contains("\"MessageAttributes\""))
        {
            // This is an SNS subscription with attributes
            try
            {
                var snsMessage = WrapperHelpers.DeserializeObject<SnsMessage>((string)record.Body);
                foreach (var messageAttribute in snsMessage.MessageAttributes)
                {
                    sqsHeaders.Add(messageAttribute.Key, messageAttribute.Value.Value);
                }
            }
            catch (Exception e)
            {
                if (_sqsTracingHeadersParsingMaxExceptionsToLog > 0)
                {
                    agent.Logger.Log(Logging.Level.Debug, $"Caught exception in TryParseSQSDistributedTraceHeaders: {e.Message}");
                    _sqsTracingHeadersParsingMaxExceptionsToLog--;
                }
            }
        }

        transaction.AcceptDistributedTraceHeaders(sqsHeaders, GetHeaderValue, TransportType.Queue);
    }

    private static void TryParseSNSDistributedTraceHeaders(dynamic snsEvent, ITransaction transaction)
    {
        // We can't pass anything dynamic to AcceptDTHeaders, so we have to copy the sns message attributes
        // to a new <string,string> dict which is then passed to AcceptDTHeaders
        IDictionary<string, string> snsHeaders = new Dictionary<string, string>();

        var record = snsEvent.Records[0];
        if (record.Sns.MessageAttributes != null)
        {
            foreach (var attribute in record.Sns.MessageAttributes)
            {
                snsHeaders.Add(attribute.Key, attribute.Value.Value);

            }
        }
        transaction.AcceptDistributedTraceHeaders(snsHeaders, GetHeaderValue, TransportType.Other);
    }

    private static void SetWebRequestProperties(IAgent agent, ITransaction transaction, dynamic webReqEvent, AwsLambdaEventType eventType)
    {
        //HTTP headers
        IDictionary<string, string> headers = webReqEvent.Headers;
        Func<IDictionary<string, string>, string, string> headersGetter = (h, k) => h[k];

        if (eventType != AwsLambdaEventType.APIGatewayHttpApiV2ProxyRequest) // v2 doesn't have MultiValueHeaders
        {
            IDictionary<string, IList<string>> multiValueHeaders = webReqEvent.MultiValueHeaders;
            Func<IDictionary<string, IList<string>>, string, string> multiValueHeadersGetter = (h, k) => string.Join(",", h[k]);

            if (multiValueHeaders != null)
            {
                transaction.SetRequestHeaders(multiValueHeaders, agent.Configuration.AllowAllRequestHeaders ? multiValueHeaders.Keys : Statics.DefaultCaptureHeaders, multiValueHeadersGetter);

                // DT transport comes from the X-Forwarded-Proto header, if present
                var forwardedProto = GetMultiHeaderValue(multiValueHeaders, xForwardedProtoHeader).FirstOrDefault();
                var dtTransport = GetDistributedTransportType(forwardedProto);

                transaction.AcceptDistributedTraceHeaders(multiValueHeaders, GetMultiHeaderValue, dtTransport);
            }
        }

        if (headers != null)
        {
            transaction.SetRequestHeaders(headers, agent.Configuration.AllowAllRequestHeaders ? webReqEvent.Headers?.Keys : Statics.DefaultCaptureHeaders, headersGetter);

            // DT transport comes from the X-Forwarded-Proto header, if present
            var forwardedProto = GetHeaderValue(headers, xForwardedProtoHeader).FirstOrDefault();
            var dtTransport = GetDistributedTransportType(forwardedProto);

            transaction.AcceptDistributedTraceHeaders(headers, GetHeaderValue, dtTransport);
        }

        if (eventType == AwsLambdaEventType.APIGatewayHttpApiV2ProxyRequest) // v2 buries method and path 
        {
            var reqContext = webReqEvent.RequestContext;
            transaction.SetRequestMethod(reqContext.Http.Method);
            transaction.SetUri(reqContext.Http.Path);
        }
        else
        {
            transaction.SetRequestMethod(webReqEvent.HttpMethod);
            transaction.SetUri(webReqEvent.Path);
        }

        if (webReqEvent.QueryStringParameters != null)
            transaction.SetRequestParameters(webReqEvent.QueryStringParameters);
    }

    private static TransportType GetDistributedTransportType(string forwardedProto)
    {
        if (forwardedProto != null)
        {
            switch (forwardedProto.ToLower())
            {
                case "http":
                    return TransportType.HTTP;
                case "https":
                    return TransportType.HTTPS;
            }
        }

        return TransportType.Unknown;
    }

    // DT getter for generic <string,string> header dict
    private static IEnumerable<string> GetHeaderValue(IDictionary<string, string> headers, string key)
    {
        var headerValues = new List<string>();
        foreach (var item in headers)
        {
            if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                headerValues.Add(item.Value);
            }
        }

        return headerValues;
    }

    // DT getter for web event multiValueHeaders
    private static IEnumerable<string> GetMultiHeaderValue(IDictionary<string, IList<string>> multiValueHeaders, string key)
    {
        var headerValues = new List<string>();
        foreach (var item in multiValueHeaders)
        {
            if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                headerValues.Add(string.Join(",", item.Value));
            }
        }

        return headerValues;
    }

    private static string TryParseAttributeFromSqsMessageBody(string body, string key)
    {
        if (!body.Contains(key))
        {
            return null;
        }
        var newrelicIndex = body.IndexOf(key, StringComparison.InvariantCultureIgnoreCase) + key.Length;
        var startIndex = body.IndexOf(SQS_MSG_ATTR_VALUE_PREFIX, newrelicIndex, StringComparison.InvariantCultureIgnoreCase) + SQS_MSG_ATTR_VALUE_PREFIX.Length;
        var endIndex = body.IndexOf('"', startIndex);
        var payload = body.Substring(startIndex, endIndex - startIndex);

        return payload;
    }

}

