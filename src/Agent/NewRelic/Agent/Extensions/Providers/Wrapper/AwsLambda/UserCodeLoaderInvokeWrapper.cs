// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//using Amazon.Lambda.Core;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.Logging;
using System.IO;
using System.Threading.Tasks;
//using Newtonsoft.Json;

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
            dynamic lambdaContext = instrumentedMethodCall.MethodCall.MethodArguments[1]; //ILambdaContext

            var typeInfo = inputObject.GetType();

            // This message doesn't seem to make it to the agent log file
            Log.Info($"input object type info = {typeInfo.FullName}");

            Stream inputStream = (Stream) inputObject;

            if (inputStream.CanRead)
            {
                using (StreamReader reader = new StreamReader(inputStream))
                {
                    var inputText = reader.ReadToEnd();
                    File.WriteAllText("/tmp/inputStreamFromWrapper.txt", inputText); // just trying to see what the contents look like
                    Log.Info($"{inputText}"); // same here, but logging from wrappers doesn't seem to be working
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
                Log.Info("Unable to read from input stream");
            }

            transaction = agent.CreateTransaction(
                isWeb: true, // will need to parse this from the input stream data per the spec...only inputs of type APIGatewayProxyRequest and ALBTargetGroupRequest should create web transactions
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: (string)lambdaContext.FunctionName,
                doNotTrackAsUnitOfWork: true);

            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, "LambdaSegmentName");


            return Delegates.GetAsyncDelegateFor<Task>(agent, segment, TaskContinueWithOption.None);
        }

    }
}
