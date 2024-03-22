// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Extensions.Lambda;

public static class LambdaEventHelpers
{
    public static void AddEventTypeAttributes(IAgent agent, ITransaction transaction, AwsLambdaEventType eventType, object inputObject, Dictionary<string, string> attributes)
    {
        switch (eventType)
        {
            case AwsLambdaEventType.APIGatewayProxyRequest:
                dynamic apiReqEvent = inputObject; // Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest
                SetWebRequestProperties(agent, transaction, apiReqEvent);

                dynamic requestContext = apiReqEvent.RequestContext;
                // arn is not available
                attributes.AddEventSourceAttribute("accountId", (string)requestContext.AccountId);
                attributes.AddEventSourceAttribute("apiId", (string)requestContext.ApiId);
                attributes.AddEventSourceAttribute("resourceId", (string)requestContext.ResourceId);
                attributes.AddEventSourceAttribute("resourcePath", (string)requestContext.ResourcePath);
                attributes.AddEventSourceAttribute("stage", (string)requestContext.Stage);

                TryParseWebRequestDistributedTraceHeaders(apiReqEvent, attributes);
                break;

            case AwsLambdaEventType.ApplicationLoadBalancerRequest:
                dynamic albReqEvent = inputObject; //Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest

                SetWebRequestProperties(agent, transaction, albReqEvent);

                attributes.AddEventSourceAttribute("arn", (string)albReqEvent.RequestContext.Elb.TargetGroupArn);
                TryParseWebRequestDistributedTraceHeaders(albReqEvent, attributes);
                break;

            case AwsLambdaEventType.CloudWatchScheduledEvent:
                dynamic cloudWatchScheduledEvent = inputObject; //Amazon.Lambda.CloudWatchEvents.ScheduledEvents.ScheduledEvent

                attributes.AddEventSourceAttribute("arn", (string)cloudWatchScheduledEvent.Resources[0]);
                attributes.AddEventSourceAttribute("account", (string)cloudWatchScheduledEvent.Account);
                attributes.AddEventSourceAttribute("id", (string)cloudWatchScheduledEvent.Id);
                attributes.AddEventSourceAttribute("region", (string)cloudWatchScheduledEvent.Region);
                attributes.AddEventSourceAttribute("resource", (string)cloudWatchScheduledEvent.Resources[0]);
                attributes.AddEventSourceAttribute("time", (string)cloudWatchScheduledEvent.Time);
                break;

            case AwsLambdaEventType.KinesisStreamingEvent:
                dynamic kinesisStreamingEvent = inputObject; //Amazon.Lambda.KinesisEvents.KinesisEvent

                attributes.AddEventSourceAttribute("arn", (string)kinesisStreamingEvent.Records[0].EventSourceArn);
                attributes.AddEventSourceAttribute("length", (string)kinesisStreamingEvent.Records.Count.ToString());
                attributes.AddEventSourceAttribute("region", (string)kinesisStreamingEvent.Records[0].AwsRegion);
                break;

            case AwsLambdaEventType.KinesisFirehoseEvent:
                dynamic kinesisFirehoseEvent = inputObject; //Amazon.Lambda.KinesisFirehoseEvents.KinesisFirehoseEvent

                attributes.AddEventSourceAttribute("arn", (string)kinesisFirehoseEvent.DeliveryStreamArn);
                attributes.AddEventSourceAttribute("length", (string)kinesisFirehoseEvent.Records.Count.ToString());
                attributes.AddEventSourceAttribute("region", (string)kinesisFirehoseEvent.Region);
                break;

            case AwsLambdaEventType.S3Event:
                dynamic s3Event = inputObject; //Amazon.Lambda.S3Events.S3Event

                attributes.AddEventSourceAttribute("arn", (string)s3Event.Records[0].S3.Bucket.Arn);
                attributes.AddEventSourceAttribute("length", (string)s3Event.Records.Count.ToString());
                attributes.AddEventSourceAttribute("region", (string)s3Event.Records[0].AwsRegion);
                attributes.AddEventSourceAttribute("eventName", (string)s3Event.Records[0].EventName);
                attributes.AddEventSourceAttribute("eventTime", (string)s3Event.Records[0].EventTime);
                attributes.AddEventSourceAttribute("xAmzId2", (string)s3Event.Records[0].ResponseElements.XAmzId2);
                attributes.AddEventSourceAttribute("bucketName", (string)s3Event.Records[0].S3.Bucket.Name);
                attributes.AddEventSourceAttribute("objectKey", (string)s3Event.Records[0].S3.Object.Key);
                attributes.AddEventSourceAttribute("objectSequencer", (string)s3Event.Records[0].S3.Object.Sequencer);
                attributes.AddEventSourceAttribute("objectSize", (string)s3Event.Records[0].S3.Object.Size.ToString());
                break;

            case AwsLambdaEventType.SimpleEmailEvent:
                dynamic sesEvent = inputObject; //Amazon.Lambda.SimpleEmailEvents.SimpleEmailEvent

                // arn is not available
                attributes.AddEventSourceAttribute("length", (string)sesEvent.Records.Count.ToString());
                attributes.AddEventSourceAttribute("date", (string)sesEvent.Records[0].Ses.Mail.CommonHeaders.Date);
                attributes.AddEventSourceAttribute("messageId", (string)sesEvent.Records[0].Ses.Mail.CommonHeaders.MessageId);
                attributes.AddEventSourceAttribute("returnPath", (string)sesEvent.Records[0].Ses.Mail.CommonHeaders.ReturnPath);
                break;

            case AwsLambdaEventType.SNSEvent:
                dynamic snsEvent = inputObject; //Amazon.Lambda.SNSEvents.SNSEvent

                attributes.AddEventSourceAttribute("arn", (string)snsEvent.Records[0].EventSubscriptionArn);
                attributes.AddEventSourceAttribute("length", (string)snsEvent.Records.Count.ToString());
                attributes.AddEventSourceAttribute("messageId", (string)snsEvent.Records[0].Sns.MessageId);
                attributes.AddEventSourceAttribute("timestamp", (string)snsEvent.Records[0].Sns.Timestamp);
                attributes.AddEventSourceAttribute("topicArn", (string)snsEvent.Records[0].Sns.TopicArn);
                attributes.AddEventSourceAttribute("type", (string)snsEvent.Records[0].Sns.Type);

                TryParseSNSDistributedTraceHeaders(snsEvent, attributes);
                break;

            case AwsLambdaEventType.SQSEvent:
                dynamic sqsEvent = inputObject; //Amazon.Lambda.SQSEvents.SQSEvent

                attributes.AddEventSourceAttribute("arn", (string)sqsEvent.Records[0].EventSourceArn);
                attributes.AddEventSourceAttribute("length", (string)sqsEvent.Records.Count.ToString());

                TryParseSQSDistributedTraceHeaders(sqsEvent, attributes);
                break;

            case AwsLambdaEventType.Unknown:
                break; // nothing to do for unknown event type

            default:
                throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Unexpected eventType");
        }
    }

    private const string NEWRELIC_TRACE_HEADER = "newrelic";

    // TODO: based on OpenTracing.AmazonLambda.Wrapper.IOParser.cs, no idea if it's correct for current use or not
    private static void TryParseWebRequestDistributedTraceHeaders(dynamic webRequestEvent, Dictionary<string, string> attributes)
    {
        IList<string> headerValues = null;
        string headerValue = null;

        if (webRequestEvent.MultiValueHeaders != null && ((IDictionary<string, IList<string>>)webRequestEvent.MultiValueHeaders).TryGetValue(NEWRELIC_TRACE_HEADER, out headerValues) && headerValues != null)
        {
            attributes.Add(NEWRELIC_TRACE_HEADER, string.Join(",", headerValues));
        }
        else if (webRequestEvent.Headers != null && ((IDictionary<string, string>)webRequestEvent.Headers).TryGetValue(NEWRELIC_TRACE_HEADER, out headerValue) && !string.IsNullOrEmpty(headerValue))
        {
            attributes.Add(NEWRELIC_TRACE_HEADER, headerValue);
        }
    }

    // TODO: based on OpenTracing.AmazonLambda.Wrapper.IOParser.cs, no idea if it's correct for current use or not
    private static void TryParseSQSDistributedTraceHeaders(dynamic sqsEvent, Dictionary<string, string> attributes)
    {
        var record = sqsEvent.Records[0];
        dynamic traceHeader = null;
        if (record.MessageAttributes != null && ((Dictionary<string, dynamic>)record.MessageAttributes).TryGetValue(NEWRELIC_TRACE_HEADER, out traceHeader))
        {
            attributes.Add(NEWRELIC_TRACE_HEADER, traceHeader.StringValue);
        }
        else if (record.Body != null && record.Body.Contains("\"Type\" : \"Notification\"") && record.Body.Contains("\"MessageAttributes\""))
        {
            // This is an SNS subscription with attributes
            var newrelicIndex = record.Body.IndexOf("newrelic", System.StringComparison.InvariantCultureIgnoreCase) + 9;
            var startIndex = record.Body.IndexOf("Value\":\"", newrelicIndex, System.StringComparison.InvariantCultureIgnoreCase) + 8;
            var endIndex = record.Body.IndexOf('"', startIndex);
            var payload = record.Body.Substring(startIndex, endIndex - startIndex);
            attributes.Add(NEWRELIC_TRACE_HEADER, (string)payload);
        }
    }

    // TODO: based on OpenTracing.AmazonLambda.Wrapper.IOParser.cs, no idea if it's correct for current use or not
    private static void TryParseSNSDistributedTraceHeaders(dynamic snsEvent, Dictionary<string, string> attributes)
    {
        var record = snsEvent.Records[0];
        dynamic traceHeader = null;
        if (record.Sns.MessageAttributes != null && ((Dictionary<string, dynamic>)record.Sns.MessageAttributes).TryGetValue(NEWRELIC_TRACE_HEADER, out traceHeader))
        {
            attributes.Add(NEWRELIC_TRACE_HEADER, (string)traceHeader.Value);
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
        else
            transaction.SetRequestHeaders(headers, agent.Configuration.AllowAllRequestHeaders ? webReqEvent.Headers.Keys : Statics.DefaultCaptureHeaders, headersGetter);

        transaction.SetRequestMethod(webReqEvent.HttpMethod);
        transaction.SetUri(webReqEvent.Path); // TODO: not sure if this is correct
        transaction.SetRequestParameters(webReqEvent.QueryStringParameters);
    }

}
