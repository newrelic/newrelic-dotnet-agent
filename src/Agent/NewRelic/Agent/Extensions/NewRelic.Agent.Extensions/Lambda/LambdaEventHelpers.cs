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

                if (apiReqEvent.RequestContext != null)
                {
                    dynamic requestContext = apiReqEvent.RequestContext;
                    // arn is not available
                    attributes.AddEventSourceAttribute("accountId", (string)requestContext.AccountId);
                    attributes.AddEventSourceAttribute("apiId", (string)requestContext.ApiId);
                    attributes.AddEventSourceAttribute("resourceId", (string)requestContext.ResourceId);
                    attributes.AddEventSourceAttribute("resourcePath", (string)requestContext.ResourcePath);
                    attributes.AddEventSourceAttribute("stage", (string)requestContext.Stage);
                }
                break;

            case AwsLambdaEventType.ApplicationLoadBalancerRequest:
                dynamic albReqEvent = inputObject; //Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest

                SetWebRequestProperties(agent, transaction, albReqEvent);

                attributes.AddEventSourceAttribute("arn", (string)albReqEvent.RequestContext.Elb.TargetGroupArn);
                break;

            case AwsLambdaEventType.CloudWatchScheduledEvent:
                dynamic cloudWatchScheduledEvent = inputObject; //Amazon.Lambda.CloudWatchEvents.ScheduledEvents.ScheduledEvent

                attributes.AddEventSourceAttribute("arn", (string)cloudWatchScheduledEvent.Resources[0]);
                attributes.AddEventSourceAttribute("account", (string)cloudWatchScheduledEvent.Account);
                attributes.AddEventSourceAttribute("id", (string)cloudWatchScheduledEvent.Id);
                attributes.AddEventSourceAttribute("region", (string)cloudWatchScheduledEvent.Region);
                attributes.AddEventSourceAttribute("resource", (string)cloudWatchScheduledEvent.Resources[0]);
                attributes.AddEventSourceAttribute("time", ((DateTime)cloudWatchScheduledEvent.Time).ToString());
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
                attributes.AddEventSourceAttribute("eventTime", ((DateTime)s3Event.Records[0].EventTime).ToString());
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
                attributes.AddEventSourceAttribute("timestamp", ((DateTime)snsEvent.Records[0].Sns.Timestamp).ToString());
                attributes.AddEventSourceAttribute("topicArn", (string)snsEvent.Records[0].Sns.TopicArn);
                attributes.AddEventSourceAttribute("type", (string)snsEvent.Records[0].Sns.Type);

                TryParseSNSDistributedTraceHeaders(snsEvent, transaction);
                break;

            case AwsLambdaEventType.SQSEvent:
                dynamic sqsEvent = inputObject; //Amazon.Lambda.SQSEvents.SQSEvent

                attributes.AddEventSourceAttribute("arn", (string)sqsEvent.Records[0].EventSourceArn);
                attributes.AddEventSourceAttribute("length", (string)sqsEvent.Records.Count.ToString());

                TryParseSQSDistributedTraceHeaders(sqsEvent, transaction);
                break;

            case AwsLambdaEventType.Unknown:
                break; // nothing to do for unknown event type

            default:
                throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Unexpected eventType");
        }
    }

    private const string NEWRELIC_TRACE_HEADER = "newrelic";

    // TODO: does this need to handle W3C trace context headers as well?
    private static void TryParseSQSDistributedTraceHeaders(dynamic sqsEvent, ITransaction transaction)
    {
        // We can't pass anything dynamic to AcceptDTHeaders, so we have to copy the sqs
        // message attributes to a new <string,string> dict and then pass that to AcceptDTHeaders
        var sqsHeaders = new Dictionary<string, string>();

        var record = sqsEvent.Records[0];
        if (record.MessageAttributes != null && record.MessageAttributes.ContainsKey(NEWRELIC_TRACE_HEADER))
        {
            sqsHeaders.Add(NEWRELIC_TRACE_HEADER, record.MessageAttributes[NEWRELIC_TRACE_HEADER].StringValue);
        }
        else if (record.Body != null && record.Body.Contains("\"Type\" : \"Notification\"") && record.Body.Contains("\"MessageAttributes\""))
        {
            // This is an SNS subscription with attributes
            var newrelicIndex = record.Body.IndexOf("newrelic", System.StringComparison.InvariantCultureIgnoreCase) + 9;
            var startIndex = record.Body.IndexOf("Value\":\"", newrelicIndex, System.StringComparison.InvariantCultureIgnoreCase) + 8;
            var endIndex = record.Body.IndexOf('"', startIndex);
            var payload = record.Body.Substring(startIndex, endIndex - startIndex);
            sqsHeaders.Add(NEWRELIC_TRACE_HEADER, (string)payload);
        }

        transaction.AcceptDistributedTraceHeaders(sqsHeaders, GetHeaderValue, TransportType.Queue);
    }

    private static void TryParseSNSDistributedTraceHeaders(dynamic snsEvent, ITransaction transaction)
    {
        // We can't pass anything dynamic to AcceptDTHeaders, so we have to copy the sns message attributes
        // to a new <string,string> dict which is then passed to AcceptDTHeaders
        var snsHeaders = new Dictionary<string, string>();

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

    private static void SetWebRequestProperties(IAgent agent, ITransaction transaction, dynamic webReqEvent)
    {
        //HTTP headers
        IDictionary<string, string> headers = webReqEvent.Headers;
        Func<IDictionary<string, string>, string, string> headersGetter = (h, k) => h[k];

        IDictionary<string, IList<string>> multiValueHeaders = webReqEvent.MultiValueHeaders;
        Func<IDictionary<string, IList<string>>, string, string> multiValueHeadersGetter = (h, k) => string.Join(",", h[k]);

        if (multiValueHeaders != null)
        {
            transaction.SetRequestHeaders(multiValueHeaders, agent.Configuration.AllowAllRequestHeaders ? multiValueHeaders.Keys : Statics.DefaultCaptureHeaders, multiValueHeadersGetter);
            transaction.AcceptDistributedTraceHeaders(multiValueHeaders, GetMultiHeaderValue, TransportType.HTTP);
        }
        else if (headers != null)
        {
            transaction.SetRequestHeaders(headers, agent.Configuration.AllowAllRequestHeaders ? webReqEvent.Headers?.Keys : Statics.DefaultCaptureHeaders, headersGetter);
            transaction.AcceptDistributedTraceHeaders(headers, GetHeaderValue, TransportType.HTTP);
        }

        transaction.SetRequestMethod(webReqEvent.HttpMethod);
        transaction.SetUri(webReqEvent.Path);
        transaction.SetRequestParameters(webReqEvent.QueryStringParameters);
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

}
