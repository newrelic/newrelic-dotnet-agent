// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//using Amazon.Lambda.Core;

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NewRelic.Reflection;

//using Newtonsoft.Json;

namespace NewRelic.Providers.Wrapper.AwsLambda
{
    public class UserCodeLoaderInvokeWrapper : IWrapper
    {
        private static Func<object, string> _functionNameGetter;
        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse("NewRelic.Providers.Wrapper.AwsLambda.UserCodeLoaderInvoke".Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {

            var inputObject = instrumentedMethodCall.MethodCall.MethodArguments[0];
            object lambdaContext = instrumentedMethodCall.MethodCall.MethodArguments[1]; //ILambdaContext

            var functionNameGetter = _functionNameGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>("Amazon.Lambda.RuntimeSupport", "Amazon.Lambda.RuntimeSupport.LambdaContext", "FunctionName");
            var functionName = functionNameGetter.Invoke(lambdaContext);

            var typeInfo = inputObject.GetType();
            agent.Logger.Log(Agent.Extensions.Logging.Level.Debug,$"input object type info = {typeInfo.FullName}");

            Stream inputStream = (Stream) inputObject;
            if (inputStream.CanRead && inputStream.CanSeek)
            {
                using (StreamReader reader = new StreamReader(inputStream, Encoding.UTF8, false, 8000, true))
                {
                    var inputText = reader.ReadToEnd();
                    agent.Logger.Log(Agent.Extensions.Logging.Level.Info,$"inputStream Contents: {inputText}");
                }
                inputStream.Seek(0, SeekOrigin.Begin); // set the stream position back to the beginning so the Lambda runtime can still use it

                // The code below blew up the wrapper with an assembly load exception on Newtonsoft.Json v13.0.0.0
                //var serializer = new JsonSerializer();
                //using (var sr = new StreamReader(inputStream))
                //using (var jsonTextReader = new JsonTextReader(sr))
                //{
                //    var jsonObject = serializer.Deserialize(jsonTextReader);
                //    foreach (var prop in jsonObject.GetType().GetProperties())
                //    {
                //        Log.Info($"input object property name={prop.Name}, type={prop.GetType().Name}");
                //    }
                //}
            }
            else
            {
                agent.Logger.Log(Agent.Extensions.Logging.Level.Info,"Unable to read from input stream");
            }

            transaction = agent.CreateTransaction(
                isWeb: true, // TODO will need to parse this from the input stream data per the spec...only inputs of type APIGatewayProxyRequest and ALBTargetGroupRequest should create web transactions
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: functionName,
                doNotTrackAsUnitOfWork: true);

            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, "LambdaSegmentName");


            return Delegates.GetAsyncDelegateFor<Task>(agent, segment, TaskContinueWithOption.None);
        }

    }
}
