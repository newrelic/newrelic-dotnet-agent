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

            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var handlerWrapper = GetHandlerWrapper())
            using (var bootstrap = new LambdaBootstrap(handlerWrapper))
            {
                _ = bootstrap.RunAsync(cancellationTokenSource.Token);

                AppLifecycleManager.CreatePidFile();

                AppLifecycleManager.WaitForTestCompletion(_port);

                cancellationTokenSource.Cancel();
            }
        }

        private static HandlerWrapper GetHandlerWrapper()
        {
            var handlerName = GetHandlerName();

            var handlerParts = handlerName.Split("::");
            if (handlerParts.Length != 3)
            {
                throw new Exception("The handler name should be in the format 'AssemblyName::Namespace.ClassName::MethodName'.");
            }

            var handlerMethodName = handlerParts[2];
            switch (handlerMethodName)
            {
                case nameof(SnsHandler):
                    return HandlerWrapper.GetHandlerWrapper<SNSEvent>(SnsHandler, new DefaultLambdaJsonSerializer());
                default:
                    throw new Exception("An unknown lambda method name was requested.");
            }
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

        public static void SnsHandler(SNSEvent evnt, ILambdaContext _)
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(100));
        }
    }
}
