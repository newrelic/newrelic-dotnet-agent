// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.AwsLambda
{
    public class HandlerMethodWrapper : IWrapper
    {
        public List<string> WebResponseHeaders = ["Content-Type", "Content-Length"];

        private static Func<object, object> _getRequestResponseFromGeneric;
        private static Func<object, string> _getFunctionNameFromLambdaContext;
        private static Func<object, string> _getFunctionVersionFromLambdaContext;
        private static Func<object, string> _getAwsRequestIdFromLambdaContext;
        private static Func<object, string> _getInvokedFunctionArnFromLambdaContext;


        public bool IsTransactionRequired => false;

        private static bool _coldStart = true;
        private static bool IsColdStart => _coldStart && !(_coldStart = false);

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse("NewRelic.Providers.Wrapper.AwsLambda.HandlerMethod".Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var isAsync = instrumentedMethodCall.IsAsync;

            var inputObject = instrumentedMethodCall.MethodCall.MethodArguments[0];
            var lambdaContext = instrumentedMethodCall.MethodCall.MethodArguments[1]; // TODO handle case where this doesn't exist

            var functionNameGetter = _getFunctionNameFromLambdaContext ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(lambdaContext.GetType(), "FunctionName");
            var functionVersionGetter = _getFunctionVersionFromLambdaContext ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(lambdaContext.GetType(), "FunctionVersion");
            var requestIdGetter = _getAwsRequestIdFromLambdaContext ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(lambdaContext.GetType(), "AwsRequestId");
            var functionArnGetter = _getInvokedFunctionArnFromLambdaContext ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(lambdaContext.GetType(), "InvokedFunctionArn");

            agent.Logger.Log(Agent.Extensions.Logging.Level.Debug, $"input object fullname = {inputObject.GetType().FullName}");

            var eventType = inputObject.GetType().ToEventType();
            agent.Logger.Log(Agent.Extensions.Logging.Level.Debug, $"eventType = {eventType}");

            var lambdaFunctionName = functionNameGetter(lambdaContext);
            var lambdaFunctionArn = functionArnGetter(lambdaContext);
            var lambdaFunctionVersion = functionVersionGetter(lambdaContext);

            transaction = agent.CreateTransaction(
                isWeb: eventType.IsWebEvent(),
                category: "Lambda", // TODO: is this is correct/useful?
                transactionDisplayName: lambdaFunctionName,
                doNotTrackAsUnitOfWork: true);

            var attributes = new Dictionary<string, string>();

            attributes.AddEventSourceAttribute("eventType", eventType.ToEventTypeString());

            attributes.Add("aws.requestId", requestIdGetter(lambdaContext));
            attributes.Add("aws.lambda.arn", lambdaFunctionArn);

            if (IsColdStart) // only report this attribute if it's a cold start
                attributes.Add("aws.coldStart", "true");

            agent.SetServerlessParameters(lambdaFunctionVersion ?? "$LATEST", lambdaFunctionArn);

            AddEventTypeAttributes(agent, transaction, eventType, inputObject, attributes);

            transaction.AddLambdaAttributes(attributes, agent);

            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, lambdaFunctionName);

            if (isAsync)
            {
                return Delegates.GetAsyncDelegateFor<Task>(agent, segment, true, InvokeTryProcessResponse, TaskContinuationOptions.ExecuteSynchronously);

                void InvokeTryProcessResponse(Task responseTask)
                {
                    if (!ValidTaskResponse(responseTask) || (segment == null))
                    {
                        return;
                    }
                    var responseGetter = _getRequestResponseFromGeneric ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(responseTask.GetType(), "Result");
                    var response = responseGetter(responseTask);
                    CaptureResponseData(transaction, response);
                }
            }
            else
            {
                return Delegates.GetDelegateFor<object>(
                        onSuccess: response =>
                        {
                            CaptureResponseData(transaction, response);

                            segment.End();
                            transaction.End();
                        },
                        onFailure: exception =>
                        {
                            segment.End(exception);
                            transaction.End();
                        });
            }
        }

        private static void AddEventTypeAttributes(IAgent agent, ITransaction transaction, AwsLambdaEventType eventType, object inputObject, Dictionary<string, string> attributes)
        {
            switch (eventType)
            {
                case AwsLambdaEventType.APIGatewayProxyRequest:
                    dynamic apiReqEvent = inputObject; // Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest
                    SetWebRequestProperties(agent, transaction, apiReqEvent);

                    dynamic requestContext = apiReqEvent.RequestContext;
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
                case AwsLambdaEventType.SNSEvent:
                    dynamic snsEvent = inputObject; //Amazon.Lambda.SNSEvents.SNSEvent

                    attributes.AddEventSourceAttribute("arn", (string)snsEvent.Records[0].EventSubscriptionArn);
                    attributes.AddEventSourceAttribute("length", (string)snsEvent.Records.Count.ToString());
                    attributes.AddEventSourceAttribute("messageId", (string)snsEvent.Records[0].Sns.MessageId);
                    attributes.AddEventSourceAttribute("topicArn", (string)snsEvent.Records[0].Sns.TopicArn);
                    attributes.AddEventSourceAttribute("timestamp", (string)snsEvent.Records[0].Sns.Timestamp);
                    attributes.AddEventSourceAttribute("type", (string)snsEvent.Records[0].Sns.Type);
                    TryParseSNSDistributedTraceHeaders(snsEvent, attributes);
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
                    attributes.AddEventSourceAttribute("date", (string)sesEvent.Records[0].Mail.CommonHeaders.Date);
                    attributes.AddEventSourceAttribute("messageId", (string)sesEvent.Records[0].Mail.CommonHeaders.MessageId);
                    attributes.AddEventSourceAttribute("returnPath", (string)sesEvent.Records[0].Mail.CommonHeaders.ReturnPath);
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

        private static void TryParseWebRequestDistributedTraceHeaders(dynamic webRequestEvent, Dictionary<string, string> attributes)
        {
            IList<string> headerValues = null;
            string headerValue = null;
            if (webRequestEvent.MultiValueHeaders != null && ((IDictionary<string, IList<string>>)webRequestEvent.MultiValueHeaders).TryGetValue(NEWRELIC_TRACE_HEADER, out headerValues))
            {
                attributes.Add(NEWRELIC_TRACE_HEADER, string.Join(",", headerValues));
            }
            if (webRequestEvent.Headers != null && ((IDictionary<string, string>)webRequestEvent.Headers).TryGetValue(NEWRELIC_TRACE_HEADER, out headerValue))
            {
                attributes.Add(NEWRELIC_TRACE_HEADER, headerValue);
            }
        }
        private static void TryParseSQSDistributedTraceHeaders(dynamic sqsEvent, Dictionary<string, string> attributes)
        {
            var record = sqsEvent.Records[0];
            if (((Dictionary<string, dynamic>)record.MessageAttributes).TryGetValue(NEWRELIC_TRACE_HEADER, out var traceHeader))
            {
                attributes.Add(NEWRELIC_TRACE_HEADER, traceHeader.StringValue);
            }
            else if (record.Body != null && record.Body.Contains("\"Type\" : \"Notification\"") && record.Body.Contains("\"MessageAttributes\""))
            {
                // This is an an SNS subscription with atttributes
                var newrelicIndex = record.Body.IndexOf("newrelic", System.StringComparison.InvariantCultureIgnoreCase) + 9;
                var startIndex = record.Body.IndexOf("Value\":\"", newrelicIndex, System.StringComparison.InvariantCultureIgnoreCase) + 8;
                var endIndex = record.Body.IndexOf('"', startIndex);
                var payload = record.Body.Substring(startIndex, endIndex - startIndex);
                attributes.Add(NEWRELIC_TRACE_HEADER, (string)payload);
            }
        }
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

            //HTTP method
            transaction.SetRequestMethod(webReqEvent.HttpMethod);
            //HTTP uri
            transaction.SetUri(webReqEvent.Path); // TODO: not sure if this is correct
            //HTTP query parameters
            transaction.SetRequestParameters(webReqEvent.QueryStringParameters);
        }

        private void CaptureResponseData(ITransaction transaction, object response)
        {
            var responseTypeName = response.GetType().FullName;
            if (responseTypeName.EndsWith("APIGatewayProxyResponse") || responseTypeName.EndsWith("ApplicationLoadBalancerResponse"))
            {
                dynamic apiResponse = response;
                transaction.SetHttpResponseStatusCode(apiResponse.StatusCode); // StatusCode is a public property on both APIGatewayProxyResponse and ApplicationLoadBalancerResponse
                IDictionary<string, string> responseHeaders = apiResponse.Headers; // Headers is a public property of type IDictionary<string,string> on both types
                foreach (var header in WebResponseHeaders)
                {
                    if (responseHeaders.TryGetValue(header, out var value))
                    {
                        transaction.AddCustomAttribute(header, value);
                    }
                }
            }
        }

        private static bool ValidTaskResponse(Task response)
        {
            return response?.Status == TaskStatus.RanToCompletion;
        }

    }

    public static class LambdaAttributeExtensions
    {
        public static void AddEventSourceAttribute(this Dictionary<string, string> dict, string suffix, string value)
        {
            dict.Add($"aws.lambda.eventSource.{suffix}", value);
        }

        public static void AddLambdaAttributes(this ITransaction transaction, Dictionary<string, string> attributes, IAgent agent)
        {
            foreach (var attribute in attributes)
            {
                agent.Logger.Log(Agent.Extensions.Logging.Level.Debug, $"Lambda Attribute: {attribute.Key}={attribute.Value}"); // TODO: remove before release
                transaction.AddCustomAttribute(attribute.Key, attribute.Value); // TODO: figure out if custom attributes are correct
            }
        }
    }

    public enum AwsLambdaEventType
    {
        Unknown,
        APIGatewayProxyRequest,
        ApplicationLoadBalancerRequest,
        CloudWatchScheduledEvent,
        KinesisStreamingEvent,
        KinesisFirehoseEvent,
        SNSEvent,
        S3Event,
        SimpleEmailEvent,
        SQSEvent,
    }

    public static class AwsLambdaEventTypeExtensions
    {
        public static AwsLambdaEventType ToEventType(this Type type)
        {
            return type.FullName switch
            {
                "Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest" => AwsLambdaEventType.APIGatewayProxyRequest,
                "Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest" => AwsLambdaEventType.ApplicationLoadBalancerRequest,
                "Amazon.Lambda.CloudWatchEvents.ScheduledEvents.ScheduledEvent" => AwsLambdaEventType.CloudWatchScheduledEvent,
                "Amazon.Lambda.KinesisEvents.KinesisEvent" => AwsLambdaEventType.KinesisStreamingEvent,
                "Amazon.Lambda.KinesisFirehoseEvents.KinesisFirehoseEvent" => AwsLambdaEventType.KinesisFirehoseEvent,
                "Amazon.Lambda.SNSEvents.SNSEvent" => AwsLambdaEventType.SNSEvent,
                "Amazon.Lambda.S3Events.S3Event" => AwsLambdaEventType.S3Event,
                "Amazon.Lambda.SimpleEmailEvents.SimpleEmailEvent" => AwsLambdaEventType.SimpleEmailEvent,
                "Amazon.Lambda.SQSEvents.SQSEvent" => AwsLambdaEventType.SQSEvent,
                _ => AwsLambdaEventType.Unknown
            };
        }
        public static string ToEventTypeString(this AwsLambdaEventType eventType)
        {
            return eventType switch
            {
                AwsLambdaEventType.APIGatewayProxyRequest => "apiGateway",
                AwsLambdaEventType.ApplicationLoadBalancerRequest => "alb",
                AwsLambdaEventType.CloudWatchScheduledEvent => "cloudWatch_scheduled",
                AwsLambdaEventType.KinesisStreamingEvent => "kinesis",
                AwsLambdaEventType.KinesisFirehoseEvent => "firehose",
                AwsLambdaEventType.SNSEvent => "sns",
                AwsLambdaEventType.S3Event => "s3",
                AwsLambdaEventType.SimpleEmailEvent => "ses",
                AwsLambdaEventType.SQSEvent => "sqs",
                AwsLambdaEventType.Unknown => "Unknown",
                _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Unexpected eventType"),
            };
        }

        public static bool IsWebEvent(this AwsLambdaEventType eventType)
        {
            return eventType == AwsLambdaEventType.APIGatewayProxyRequest || eventType == AwsLambdaEventType.ApplicationLoadBalancerRequest;
        }
    }

}
