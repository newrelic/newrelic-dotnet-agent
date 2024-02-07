// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.SQSEvents;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace NewRelic.Providers.Wrapper.AwsLambda
{
    public class HandlerMethodWrapper : IWrapper
    {
        public List<string> WebInputEventTypes = ["APIGatewayProxyRequest", "ALBTargetGroupRequest"];
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

        public bool IsTransactionRequired => false;

        private bool _coldStart = true;
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

            var inputObject = instrumentedMethodCall.MethodCall.MethodArguments[0];
            var lambdaContext = (ILambdaContext) instrumentedMethodCall.MethodCall.MethodArguments[1];

            var eventTypeName = inputObject.GetType().FullName.Split('.').Last(); // e.g. SQSEvent

            var xapi = agent.GetExperimentalApi();

            xapi.LogFromWrapper($"input object type info = {eventTypeName}");

            transaction = agent.CreateTransaction(
                isWeb: WebInputEventTypes.Any(s => s == eventTypeName),
                category: "Lambda", // TODO: is this is correct/useful?
                transactionDisplayName: lambdaContext.FunctionName,
                doNotTrackAsUnitOfWork: true);

            var attributes = new Dictionary<string, string>();

            var eventType = eventTypes[eventTypeName];

            attributes.AddEventSourceAttribute("eventType", eventType);
            attributes.Add("aws.ReqeustId", lambdaContext.AwsRequestId);
            attributes.Add("aws.lambda.arn", lambdaContext.InvokedFunctionArn);
            attributes.Add("aws.coldStart", IsColdstart().ToString());

            switch (eventType)
            {
                case "apiGateway":
                    var apiReqEvent = (APIGatewayProxyRequest)inputObject;
                    //HTTP headers
                    transaction.SetRequestHeaders(apiReqEvent.Headers, agent.Configuration.AllowAllRequestHeaders ? apiReqEvent.Headers.Keys : Statics.DefaultCaptureHeaders, (headers, key) => headers[key]);
                    //HTTP method
                    transaction.SetRequestMethod(apiReqEvent.HttpMethod);
                    //HTTP uri
                    transaction.SetUri(apiReqEvent.Path); // TODO: not sure if this is correct
                    //HTTP query parameters
                    transaction.SetRequestParameters(apiReqEvent.QueryStringParameters);

                    //aws.lambda.eventSource.accountId    string  event requestContext.accountId Identifier of the API account
                    attributes.AddEventSourceAttribute("accountId", apiReqEvent.RequestContext.AccountId);
                    //aws.lambda.eventSource.apiId string event requestContext.apiId Identifier of the API gateway
                    attributes.AddEventSourceAttribute("apiId", apiReqEvent.RequestContext.ApiId);
                    //aws.lambda.eventSource.resourceId string event requestContext.resourceId Identifier of the API resource
                    attributes.AddEventSourceAttribute("resourceId", apiReqEvent.RequestContext.ResourceId);
                    //aws.lambda.eventSource.resourcePath string event requestContext.resourcePath Path of the API resource
                    attributes.AddEventSourceAttribute("resourcePath", apiReqEvent.RequestContext.ResourcePath);
                    //aws.lambda.eventSource.stage string event requestContext.stage Stage of the API resource
                    attributes.AddEventSourceAttribute("stage", apiReqEvent.RequestContext.Stage);

                    // TODO: insert distributed tracing headers if they're not already there
                    break;
                case "sqs":
                    var sqsEvent = (SQSEvent)inputObject;
                    attributes.AddEventSourceAttribute("arn", sqsEvent.Records[0].EventSourceArn);
                    attributes.AddEventSourceAttribute("length", sqsEvent.Records.Count.ToString());
                    break;
                default:
                    break;
            }

            transaction.AddLambdaAttributes(attributes, xapi);

            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, lambdaContext.FunctionName);


            return Delegates.GetAsyncDelegateFor<Task>(agent, segment, TaskContinueWithOption.None);
        }
    }

    public static class LambdaAttributeExtensions
    {
        public static void AddEventSourceAttribute(this Dictionary<string, string> dict, string suffix, string value)
        {
            dict.Add($"aws.labmda.eventSource.{suffix}", value);
        }

        public static void AddLambdaAttributes(this ITransaction transaction, Dictionary<string, string> attributes, IAgentExperimental xapi)
        {
            foreach (var attribute in attributes)
            {
                xapi.LogFromWrapper($"Lambda Attribute: {attribute.Key}={attribute.Value}");
                transaction.AddCustomAttribute(attribute.Key, attribute.Value); // TODO: figure out if custom attributes are correct
            }
        }

    }
}
