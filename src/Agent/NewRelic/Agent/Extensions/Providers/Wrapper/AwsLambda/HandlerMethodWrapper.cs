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
        public List<string> WebInputEventTypes = ["APIGatewayProxyRequest", "ALBTargetGroupRequest"];
        public List<string> WebResponseHeaders = ["Content-Type", "Content-Length"];
        public Dictionary<string, string> eventTypes = new Dictionary<string, string>()
        {
            { "APIGatewayProxyRequest", "apiGateway" },
            { "ApplicationLoadBalancerRequest", "alb" },
            { "CloudWatchEvent", "cloudWatch_scheduled" },
            { "KinesisEvent", "kinesis" },
            { "SNSEvent", "sns" },
            { "S3Event", "s3" },
            { "SimpleEmailEvent", "ses" },
            { "SQSEvent", "sqs" },
        };

        private static Func<object, object> _getRequestResponseFromGeneric;

        public bool IsTransactionRequired => false;

        private static bool _coldStart = true;
        private bool IsColdstart()
        {
            if (_coldStart)
            {
                _coldStart = false;
                return true;
            }
            return false;
        }

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse("NewRelic.Providers.Wrapper.AwsLambda.HandlerMethod".Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var isAsync = instrumentedMethodCall.IsAsync;

            var inputObject = instrumentedMethodCall.MethodCall.MethodArguments[0];
            dynamic lambdaContext = instrumentedMethodCall.MethodCall.MethodArguments[1]; // TODO handle case where this doesn't exist

            var eventTypeName = inputObject.GetType().FullName.Split('.').Last(); // e.g. SQSEvent

            agent.Logger.Log(Agent.Extensions.Logging.Level.Debug, $"input object type info = {eventTypeName}");

            transaction = agent.CreateTransaction(
                isWeb: WebInputEventTypes.Any(s => s == eventTypeName),
                category: "Lambda", // TODO: is this is correct/useful?
                transactionDisplayName: (string)lambdaContext.FunctionName,
                doNotTrackAsUnitOfWork: true);

            var attributes = new Dictionary<string, string>();

            var eventType = eventTypes[eventTypeName];

            attributes.AddEventSourceAttribute("eventType", eventType);
            attributes.Add("aws.ReqeustId", (string)lambdaContext.AwsRequestId);
            attributes.Add("aws.lambda.arn", (string)lambdaContext.InvokedFunctionArn);
            attributes.Add("aws.coldStart", IsColdstart().ToString());

            switch (eventType)
            {
                case "apiGateway":
                    dynamic apiReqEvent = inputObject; // APIGatewayProxyRequest
                    //HTTP headers
                    IDictionary<string,string> headers = apiReqEvent.Headers;
                    Func<IDictionary<string, string>, string, string> getter = (headers, key) => headers[key];
                    transaction.SetRequestHeaders(headers, agent.Configuration.AllowAllRequestHeaders ? apiReqEvent.Headers.Keys : Statics.DefaultCaptureHeaders, getter);
                    //HTTP method
                    transaction.SetRequestMethod(apiReqEvent.HttpMethod);
                    //HTTP uri
                    transaction.SetUri(apiReqEvent.Path); // TODO: not sure if this is correct
                    //HTTP query parameters
                    transaction.SetRequestParameters(apiReqEvent.QueryStringParameters);

                    //aws.lambda.eventSource.accountId    string  event requestContext.accountId Identifier of the API account
                    dynamic requestContext = apiReqEvent.RequestContext;
                    attributes.AddEventSourceAttribute("accountId", (string)requestContext.AccountId);
                    //aws.lambda.eventSource.apiId string event requestContext.apiId Identifier of the API gateway
                    attributes.AddEventSourceAttribute("apiId", (string)requestContext.ApiId);
                    //aws.lambda.eventSource.resourceId string event requestContext.resourceId Identifier of the API resource
                    attributes.AddEventSourceAttribute("resourceId", (string)requestContext.ResourceId);
                    //aws.lambda.eventSource.resourcePath string event requestContext.resourcePath Path of the API resource
                    attributes.AddEventSourceAttribute("resourcePath", (string)requestContext.ResourcePath);
                    //aws.lambda.eventSource.stage string event requestContext.stage Stage of the API resource
                    attributes.AddEventSourceAttribute("stage", (string)requestContext.Stage);

                    // TODO: insert distributed tracing headers if they're not already there
                    break;
                case "sqs":
                    dynamic sqsEvent = inputObject; //Amazon.Lambda.SQSEvents.SQSEvent
                    attributes.AddEventSourceAttribute("arn", (string)sqsEvent.Records[0].EventSourceArn);
                    attributes.AddEventSourceAttribute("length", (string)sqsEvent.Records.Count.ToString());
                    break;
                default:
                    break;
            }

            transaction.AddLambdaAttributes(attributes, agent);

            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, lambdaContext.FunctionName);

            if (isAsync)
            {
                return Delegates.GetAsyncDelegateFor<Task>(agent, segment, true, (Action<Task>)InvokeTryProcessResponse, TaskContinuationOptions.ExecuteSynchronously);

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
                        },
                        onFailure: exception =>
                        {
                            segment.End(exception);
                        });
            }
        }

        private void CaptureResponseData(ITransaction transaction, object response)
        {
            var responseTypeName = response.GetType().FullName;
            if (responseTypeName.EndsWith("APIGatewayProxyResponse") || responseTypeName.EndsWith("ApplicationLoadBalancerResponse"))
            {
                dynamic apiResponse = response;
                transaction.SetHttpResponseStatusCode((int)apiResponse.StatusCode); // StatusCode is a public property on both APIGatewayProxyResponse and ApplicationLoadBalancerResponse
                IDictionary<string,string> responseHeaders = apiResponse.Headers; // Headers is a public property of type IDictionary<string,string> on both types
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
            dict.Add($"aws.labmda.eventSource.{suffix}", value);
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
}
