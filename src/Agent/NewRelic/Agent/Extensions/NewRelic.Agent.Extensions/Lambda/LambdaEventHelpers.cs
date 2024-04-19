// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Extensions.Lambda;

public static class LambdaEventHelpers
{
    public static void AddEventTypeAttributes(IAgent agent, ITransaction transaction, AwsLambdaEventType eventType, object inputObject)
    {
        try
        {
            switch (eventType)
            {
                case AwsLambdaEventType.APIGatewayProxyRequest:
                    dynamic apiReqEvent = inputObject; // Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest
                    SetWebRequestProperties(agent, transaction, apiReqEvent);

                    if (apiReqEvent.RequestContext != null)
                    {
                        dynamic requestContext = apiReqEvent.RequestContext;
                        // arn is not available
                        transaction.AddEventSourceAttribute("accountId", (string)requestContext.AccountId);
                        transaction.AddEventSourceAttribute("apiId", (string)requestContext.ApiId);
                        transaction.AddEventSourceAttribute("resourceId", (string)requestContext.ResourceId);
                        transaction.AddEventSourceAttribute("resourcePath", (string)requestContext.ResourcePath);
                        transaction.AddEventSourceAttribute("stage", (string)requestContext.Stage);

                        TryParseWebRequestDistributedTraceHeaders(apiReqEvent, transaction);
                    }
                    break;

                case AwsLambdaEventType.ApplicationLoadBalancerRequest:
                    dynamic albReqEvent = inputObject; //Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest

                    SetWebRequestProperties(agent, transaction, albReqEvent);

                    transaction.AddEventSourceAttribute("arn", (string)albReqEvent.RequestContext.Elb.TargetGroupArn);
                    TryParseWebRequestDistributedTraceHeaders(albReqEvent, transaction);
                    break;

                case AwsLambdaEventType.CloudWatchScheduledEvent:
                    dynamic cloudWatchScheduledEvent = inputObject; //Amazon.Lambda.CloudWatchEvents.ScheduledEvents.ScheduledEvent

                    transaction.AddEventSourceAttribute("arn", (string)cloudWatchScheduledEvent.Resources[0]);
                    transaction.AddEventSourceAttribute("account", (string)cloudWatchScheduledEvent.Account);
                    transaction.AddEventSourceAttribute("id", (string)cloudWatchScheduledEvent.Id);
                    transaction.AddEventSourceAttribute("region", (string)cloudWatchScheduledEvent.Region);
                    transaction.AddEventSourceAttribute("resource", (string)cloudWatchScheduledEvent.Resources[0]);
                    // TODO: Figure out if the time value should be in some specific format. The spec doesn't say.
                    transaction.AddEventSourceAttribute("time", ((DateTime)cloudWatchScheduledEvent.Time).ToString());
                    break;

                case AwsLambdaEventType.KinesisStreamingEvent:
                    dynamic kinesisStreamingEvent = inputObject; //Amazon.Lambda.KinesisEvents.KinesisEvent

                    transaction.AddEventSourceAttribute("arn", (string)kinesisStreamingEvent.Records[0].EventSourceArn);
                    transaction.AddEventSourceAttribute("length", (string)kinesisStreamingEvent.Records.Count.ToString());
                    transaction.AddEventSourceAttribute("region", (string)kinesisStreamingEvent.Records[0].AwsRegion);
                    break;

                case AwsLambdaEventType.KinesisFirehoseEvent:
                    dynamic kinesisFirehoseEvent = inputObject; //Amazon.Lambda.KinesisFirehoseEvents.KinesisFirehoseEvent

                    transaction.AddEventSourceAttribute("arn", (string)kinesisFirehoseEvent.DeliveryStreamArn);
                    transaction.AddEventSourceAttribute("length", (string)kinesisFirehoseEvent.Records.Count.ToString());
                    transaction.AddEventSourceAttribute("region", (string)kinesisFirehoseEvent.Region);
                    break;

                case AwsLambdaEventType.S3Event:
                    dynamic s3Event = inputObject; //Amazon.Lambda.S3Events.S3Event

                    transaction.AddEventSourceAttribute("arn", (string)s3Event.Records[0].S3.Bucket.Arn);
                    transaction.AddEventSourceAttribute("length", (string)s3Event.Records.Count.ToString());
                    transaction.AddEventSourceAttribute("region", (string)s3Event.Records[0].AwsRegion);
                    transaction.AddEventSourceAttribute("eventName", (string)s3Event.Records[0].EventName);
                    // TODO: Figure out if the eventTime value should be in some specific format. The spec doesn't say.
                    transaction.AddEventSourceAttribute("eventTime", ((DateTime)s3Event.Records[0].EventTime).ToString());
                    transaction.AddEventSourceAttribute("xAmzId2", (string)s3Event.Records[0].ResponseElements.XAmzId2);
                    transaction.AddEventSourceAttribute("bucketName", (string)s3Event.Records[0].S3.Bucket.Name);
                    transaction.AddEventSourceAttribute("objectKey", (string)s3Event.Records[0].S3.Object.Key);
                    transaction.AddEventSourceAttribute("objectSequencer", (string)s3Event.Records[0].S3.Object.Sequencer);
                    transaction.AddEventSourceAttribute("objectSize", (string)s3Event.Records[0].S3.Object.Size.ToString());
                    break;

                case AwsLambdaEventType.SimpleEmailEvent:
                    dynamic sesEvent = inputObject; //Amazon.Lambda.SimpleEmailEvents.SimpleEmailEvent

                    // arn is not available
                    transaction.AddEventSourceAttribute("length", (string)sesEvent.Records.Count.ToString());
                    transaction.AddEventSourceAttribute("date", (string)sesEvent.Records[0].Ses.Mail.CommonHeaders.Date);
                    transaction.AddEventSourceAttribute("messageId", (string)sesEvent.Records[0].Ses.Mail.CommonHeaders.MessageId);
                    transaction.AddEventSourceAttribute("returnPath", (string)sesEvent.Records[0].Ses.Mail.CommonHeaders.ReturnPath);
                    break;

                case AwsLambdaEventType.SNSEvent:
                    dynamic snsEvent = inputObject; //Amazon.Lambda.SNSEvents.SNSEvent

                    transaction.AddEventSourceAttribute("arn", (string)snsEvent.Records[0].EventSubscriptionArn);
                    transaction.AddEventSourceAttribute("length", (string)snsEvent.Records.Count.ToString());
                    transaction.AddEventSourceAttribute("messageId", (string)snsEvent.Records[0].Sns.MessageId);
                    // TODO: Figure out if the timestamp value should be in some specific format. The spec doesn't say.
                    transaction.AddEventSourceAttribute("timestamp", ((DateTime)snsEvent.Records[0].Sns.Timestamp).ToString());
                    transaction.AddEventSourceAttribute("topicArn", (string)snsEvent.Records[0].Sns.TopicArn);
                    transaction.AddEventSourceAttribute("type", (string)snsEvent.Records[0].Sns.Type);

                    TryParseSNSDistributedTraceHeaders(snsEvent, transaction);
                    break;

                case AwsLambdaEventType.SQSEvent:
                    dynamic sqsEvent = inputObject; //Amazon.Lambda.SQSEvents.SQSEvent

                    transaction.AddEventSourceAttribute("arn", (string)sqsEvent.Records[0].EventSourceArn);
                    transaction.AddEventSourceAttribute("length", (string)sqsEvent.Records.Count.ToString());

                    TryParseSQSDistributedTraceHeaders(sqsEvent, transaction);
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

    private const string NEWRELIC_TRACE_HEADER = "newrelic";

    // TODO: based on OpenTracing.AmazonLambda.Wrapper.IOParser.cs, no idea if it's correct for current use or not
    private static void TryParseWebRequestDistributedTraceHeaders(dynamic webRequestEvent, ITransaction transaction)
    {
        IList<string> headerValues = null;
        string headerValue = null;

        if (webRequestEvent.MultiValueHeaders != null && ((IDictionary<string, IList<string>>)webRequestEvent.MultiValueHeaders).TryGetValue(NEWRELIC_TRACE_HEADER, out headerValues) && headerValues != null)
        {
            transaction.AddLambdaAttribute(NEWRELIC_TRACE_HEADER, string.Join(",", headerValues));
        }
        else if (webRequestEvent.Headers != null && ((IDictionary<string, string>)webRequestEvent.Headers).TryGetValue(NEWRELIC_TRACE_HEADER, out headerValue) && !string.IsNullOrEmpty(headerValue))
        {
            transaction.AddLambdaAttribute(NEWRELIC_TRACE_HEADER, headerValue);
        }
    }

    // TODO: based on OpenTracing.AmazonLambda.Wrapper.IOParser.cs, no idea if it's correct for current use or not
    private static void TryParseSQSDistributedTraceHeaders(dynamic sqsEvent, ITransaction transaction)
    {
        var record = sqsEvent.Records[0];
        if (record.MessageAttributes != null && record.MessageAttributes.ContainsKey(NEWRELIC_TRACE_HEADER))
        {
            transaction.AddLambdaAttribute(NEWRELIC_TRACE_HEADER, record.MessageAttributes[NEWRELIC_TRACE_HEADER].StringValue);
        }
        else if (record.Body != null && record.Body.Contains("\"Type\" : \"Notification\"") && record.Body.Contains("\"MessageAttributes\""))
        {
            // This is an SNS subscription with attributes
            var newrelicIndex = record.Body.IndexOf("newrelic", System.StringComparison.InvariantCultureIgnoreCase) + 9;
            var startIndex = record.Body.IndexOf("Value\":\"", newrelicIndex, System.StringComparison.InvariantCultureIgnoreCase) + 8;
            var endIndex = record.Body.IndexOf('"', startIndex);
            var payload = record.Body.Substring(startIndex, endIndex - startIndex);
            transaction.AddLambdaAttribute(NEWRELIC_TRACE_HEADER, (string)payload);
        }
    }

    // TODO: based on OpenTracing.AmazonLambda.Wrapper.IOParser.cs, no idea if it's correct for current use or not
    private static void TryParseSNSDistributedTraceHeaders(dynamic snsEvent, ITransaction transaction)
    {
        var record = snsEvent.Records[0];
        if (record.Sns.MessageAttributes != null && record.Sns.MessageAttributes.ContainsKey(NEWRELIC_TRACE_HEADER))
        {
            transaction.AddLambdaAttribute(NEWRELIC_TRACE_HEADER, (string)record.Sns.MessageAttributes[NEWRELIC_TRACE_HEADER].Value);
        }
    }

    private static void SetWebRequestProperties(IAgent agent, ITransaction transaction, dynamic webReqEvent)
    {
        //HTTP headers
        IDictionary<string, string> headers = webReqEvent.Headers;
        Func<IDictionary<string, string>, string, string> headersGetter = (h, k) => h[k];

        IDictionary<string, IList<string>> multiValueHeaders = webReqEvent.MultiValueHeaders;
        Func<IDictionary<string, IList<string>>, string, string> multiValueHeadersGetter = (h, k) => string.Join(",", h[k]);

        if (multiValueHeaders != null)
            transaction.SetRequestHeaders(multiValueHeaders, agent.Configuration.AllowAllRequestHeaders ? multiValueHeaders.Keys : Statics.DefaultCaptureHeaders, multiValueHeadersGetter);
        else if (headers != null)
            transaction.SetRequestHeaders(headers, agent.Configuration.AllowAllRequestHeaders ? webReqEvent.Headers?.Keys : Statics.DefaultCaptureHeaders, headersGetter);

        transaction.SetRequestMethod(webReqEvent.HttpMethod);
        transaction.SetUri(webReqEvent.Path); // TODO: not sure if this is correct
        transaction.SetRequestParameters(webReqEvent.QueryStringParameters);
    }
}
