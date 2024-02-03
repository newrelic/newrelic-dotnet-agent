// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.SQSEvents;
using Amazon.Lambda.Core;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Threading.Tasks;
using System.Linq;
//using Newtonsoft.Json;

namespace NewRelic.Providers.Wrapper.AwsLambda
{
    public class HandlerMethodWrapper : IWrapper
    {
        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse("NewRelic.Providers.Wrapper.AwsLambda.HandlerMethod".Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {

            var inputObject = instrumentedMethodCall.MethodCall.MethodArguments[0];
            var lambdaContext = (ILambdaContext) instrumentedMethodCall.MethodCall.MethodArguments[1];

            var eventTypeName = inputObject.GetType().FullName.Split('.').Last(); // e.g. SQSEvents.SQSEvent

            var xapi = agent.GetExperimentalApi();

            xapi.LogFromWrapper($"input object type info = {eventTypeName}");

            transaction = agent.CreateTransaction(
                isWeb: true, // will need to parse this from the input stream data per the spec...only inputs of type APIGatewayProxyRequest and ALBTargetGroupRequest should create web transactions
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: lambdaContext.FunctionName,
                doNotTrackAsUnitOfWork: true);

            // eventually make this a big switch statement
            if (eventTypeName == "SQSEvent")
            {
                var sqsEvent = (SQSEvent)inputObject;
                transaction.AddCustomAttribute("aws.lambda.eventSource.eventType", "sqs");
                transaction.AddCustomAttribute("aws.lambda.eventSource.arn", sqsEvent.Records[0].EventSourceArn);
                xapi.LogFromWrapper($"event source arn={sqsEvent.Records[0].EventSourceArn}");
                transaction.AddCustomAttribute("aws.lambda.eventSource.length", sqsEvent.Records.Count.ToString());
                xapi.LogFromWrapper($"event source length={sqsEvent.Records.Count}");
            }


            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, lambdaContext.FunctionName);


            return Delegates.GetAsyncDelegateFor<Task>(agent, segment, TaskContinueWithOption.None);
        }

    }
}
