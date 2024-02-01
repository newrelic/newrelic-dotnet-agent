// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System;
using System.Threading.Tasks;

namespace NewRelic.Providers.Wrapper.AwsLambda
{
    public class UserCodeLoaderInvokeWrapper : IWrapper
    {
        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse("NewRelic.Providers.Wrapper.AwsLambda.UserCodeLoaderInvoke".Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {

            var inputObject = instrumentedMethodCall.MethodCall.MethodArguments[0];
            var lambdaContext = (ILambdaContext) instrumentedMethodCall.MethodCall.MethodArguments[1];

            var typeInfo = inputObject.GetType();

            Console.WriteLine($"input object type info = {typeInfo.FullName}");


            //Console.WriteLine($"input stream can be read: {inputStream.CanRead}");


            transaction = agent.CreateTransaction(
                isWeb: true, // will need to parse this from the input stream data per the spec...only inputs of type APIGatewayProxyRequest and ALBTargetGroupRequest should create web transactions
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: lambdaContext.FunctionName,
                doNotTrackAsUnitOfWork: true);

            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, "LambdaSegmentName");


            return Delegates.GetAsyncDelegateFor<Task>(agent, segment, TaskContinueWithOption.None);
        }

    }
}
