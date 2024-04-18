// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SNSEvents;
using ApplicationLifecycle;

namespace LambdaSelfExecutingAssembly
{
    internal class Program
    {
        private static string _port = "";

        private static void Main(string[] args)
        {
            _port = AppLifecycleManager.GetPortFromArgs(args);

            using var cancellationTokenSource = new CancellationTokenSource();
            using var handlerWrapper = GetHandlerWrapper();
            var bootstrap = new LambdaBootstrap(handlerWrapper?.Handler ?? GetNonStandardLambdaBootstrapHandler());

            _ = bootstrap.RunAsync(cancellationTokenSource.Token);

            AppLifecycleManager.CreatePidFile();

            AppLifecycleManager.WaitForTestCompletion(_port);

            cancellationTokenSource.Cancel();
        }

        private static HandlerWrapper? GetHandlerWrapper()
        {
            var serializer = new DefaultLambdaJsonSerializer();

            var handlerMethodName = GetHandlerMethodName();
            switch (handlerMethodName)
            {
                case nameof(SnsHandler):
                    return HandlerWrapper.GetHandlerWrapper<SNSEvent>(SnsHandler, serializer);
                case nameof(SnsHandlerAsync):
                    return HandlerWrapper.GetHandlerWrapper<SNSEvent>(SnsHandlerAsync, serializer);
                case nameof(CustomEventHandler):
                    return HandlerWrapper.GetHandlerWrapper(CustomEventHandler);
                case nameof(StringInputAndOutputHandler):
                    return HandlerWrapper.GetHandlerWrapper<string, string>(StringInputAndOutputHandler, serializer);
                case nameof(StringInputAndOutputHandlerAsync):
                    return HandlerWrapper.GetHandlerWrapper<string, string>(StringInputAndOutputHandlerAsync, serializer);
                case nameof(LambdaContextOnlyHandler):
                    return HandlerWrapper.GetHandlerWrapper(LambdaContextOnlyHandler);
                case nameof(StreamParameterHandler):
                    return HandlerWrapper.GetHandlerWrapper(StreamParameterHandler);
                default:
                    return null;
            }
        }

        private static LambdaBootstrapHandler GetNonStandardLambdaBootstrapHandler()
        {
            var serializer = new DefaultLambdaJsonSerializer();

            var handlerMethodName = GetHandlerMethodName();
            switch (handlerMethodName)
            {
                case nameof(OutOfOrderParametersHandler):
                    var handler = delegate (InvocationRequest invocation)
                    {
                        string arg = serializer.Deserialize<string>(invocation.InputStream);
                        OutOfOrderParametersHandler(invocation.LambdaContext, arg);
                        return Task.FromResult(new InvocationResponse(new MemoryStream(0), disposeOutputStream: false));
                    };
                    return new LambdaBootstrapHandler(handler);
                default:
                    throw new Exception($"An unknown lambda method name was requested '{handlerMethodName}'.");
            }
        }

        private static string GetHandlerMethodName()
        {
            var handlerName = GetHandlerName();

            var handlerParts = handlerName.Split("::");
            if (handlerParts.Length != 3)
            {
                throw new Exception("The handler name should be in the format 'AssemblyName::Namespace.ClassName::MethodName'.");
            }

            return handlerParts[2];
        }
        private static string GetHandlerName()
        {
            var handlerName = Environment.GetEnvironmentVariable("NEW_RELIC_LAMBDA_FUNCTION_HANDLER") ??
                Environment.GetEnvironmentVariable("_HANDLER");

            if (string.IsNullOrWhiteSpace(handlerName))
            {
                throw new Exception("The lambda handler is missing. Please ensure that the correct handler is defined in either the NEW_RELIC_LAMBDA_FUNCTION_HANDLER or _HANDLER environment variables.");
            }

            return handlerName;
        }

        public static void SnsHandler(SNSEvent _, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(SnsHandler));
            Thread.Sleep(TimeSpan.FromMilliseconds(100));
        }

        public static async Task SnsHandlerAsync(SNSEvent _, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(SnsHandlerAsync));
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        public static void CustomEventHandler()
        {
            Console.WriteLine("Executing lambda {0}", nameof(CustomEventHandler));
            NewRelic.Api.Agent.NewRelic.RecordCustomEvent("TestLambdaCustomEvent", [new KeyValuePair<string, object>("lambdaHandler", nameof(CustomEventHandler))]);
            Thread.Sleep(TimeSpan.FromMilliseconds(100));
        }

        public static string StringInputAndOutputHandler(string input)
        {
            Console.WriteLine("Executing lambda {0}", nameof(StringInputAndOutputHandler));
            return "done with " + input;
        }

        public static async Task<string> StringInputAndOutputHandlerAsync(string input)
        {
            Console.WriteLine("Executing lambda {0}", nameof(StringInputAndOutputHandler));
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            return "done with " + input;
        }

        public static void LambdaContextOnlyHandler(ILambdaContext _)
        {
            Console.WriteLine("Executing lambda {0}", nameof(LambdaContextOnlyHandler));
        }

        public static void OutOfOrderParametersHandler(ILambdaContext _, string input)
        {
            Console.WriteLine("Executing lambda {0} with input {1}", nameof(OutOfOrderParametersHandler), input);
        }

        public static Stream StreamParameterHandler(Stream requestStream, ILambdaContext context)
        {
            var input = new DefaultLambdaJsonSerializer().Deserialize<string>(requestStream);
            Console.WriteLine("Executing lambda {0} with input {1}", nameof(StreamParameterHandler), input);
            return new MemoryStream(0);
        }
    }
}
