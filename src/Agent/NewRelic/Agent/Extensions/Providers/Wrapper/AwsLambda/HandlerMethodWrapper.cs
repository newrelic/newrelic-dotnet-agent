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
        public Dictionary<string, string> EventTypes = new()
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

            var eventTypeName = inputObject.GetType().FullName.Split('.').Last(); // e.g. SQSEvent

            agent.Logger.Log(Agent.Extensions.Logging.Level.Debug, $"input object type info = {eventTypeName}");

            var lambdaFunctionName = functionNameGetter(lambdaContext);
            var lambdaFunctionArn = functionArnGetter(lambdaContext);
            var lambdaFunctionVersion = functionVersionGetter(lambdaContext);

            transaction = agent.CreateTransaction(
                isWeb: WebInputEventTypes.Any(s => s == eventTypeName),
                category: "Lambda", // TODO: is this is correct/useful?
                transactionDisplayName: lambdaFunctionName,
                doNotTrackAsUnitOfWork: true);

            var attributes = new Dictionary<string, string>();

            EventTypes.TryGetValue(eventTypeName, out var eventType); // handle case where the name might not be in the eventType dictionary

            attributes.AddEventSourceAttribute("eventType", eventType ?? "Unknown"); // TODO: Is this correct?
            attributes.AddEventSourceAttribute("arn", "????"); // TODO: how to get this value? Spec says "ARN of the invocation source" 

            attributes.Add("aws.requestId", requestIdGetter(lambdaContext));
            attributes.Add("aws.lambda.arn", lambdaFunctionArn);

            if (IsColdStart) // only report this attribute if it's a cold start
                attributes.Add("aws.coldStart", "true");

            agent.SetServerlessParameters(lambdaFunctionVersion ?? "$LATEST", lambdaFunctionArn); // TODO: Is the default for version correct?

            switch (eventType)
            {
                case "apiGateway":
                    dynamic apiReqEvent = inputObject; // APIGatewayProxyRequest
                    //HTTP headers
                    IDictionary<string,string> headers = apiReqEvent.Headers;
                    Func<IDictionary<string, string>, string, string> getter = (h, k) => h[k];
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

        private void CaptureResponseData(ITransaction transaction, object response)
        {
            var responseTypeName = response.GetType().FullName;
            if (responseTypeName.EndsWith("APIGatewayProxyResponse") || responseTypeName.EndsWith("ApplicationLoadBalancerResponse"))
            {
                dynamic apiResponse = response;
                transaction.SetHttpResponseStatusCode(apiResponse.StatusCode); // StatusCode is a public property on both APIGatewayProxyResponse and ApplicationLoadBalancerResponse
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
}
