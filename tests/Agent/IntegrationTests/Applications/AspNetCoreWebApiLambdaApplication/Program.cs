// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using ApplicationLifecycle;
using CommandLine;

namespace AspNetCoreWebApiLambdaApplication
{
    internal class Program
    {
        private class Options
        {
            [Option("handler", Required = true, HelpText = "Handler function to use.")]
            public string Handler { get; set; }
        }

        private static string _port = "";
        private static string _handlerToInvoke = "";

        private static void Main(string[] args)
        {
            _port = AppLifecycleManager.GetPortFromArgs(args);

            _handlerToInvoke = GetHandlerFromArgs(args);

            using var cancellationTokenSource = new CancellationTokenSource();
            using var handlerWrapper = GetHandlerWrapper();

            // Instantiate a LambdaBootstrap and run it.
            // It will wait for invocations from AWS Lambda and call the handler function for each one.
            using var bootstrap = new LambdaBootstrap(handlerWrapper);

            _ = bootstrap.RunAsync(cancellationTokenSource.Token);

            AppLifecycleManager.CreatePidFile();

            AppLifecycleManager.WaitForTestCompletion(_port);

            cancellationTokenSource.Cancel();
        }

        private static string GetHandlerFromArgs(string[] args)
        {
            var handler = string.Empty;

            var commandLine = string.Join(" ", args);

            new Parser(with => { with.IgnoreUnknownArguments = true; })
                .ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    handler = o.Handler;
                });

            if (string.IsNullOrEmpty(handler))
                throw new Exception("--handler commandline argument could not be parsed.");

            return handler;
        }

        private static HandlerWrapper GetHandlerWrapper()
        {
            switch (_handlerToInvoke)
            {
                case "APIGatewayProxyFunctionEntryPoint":
                    {
                        var entryPoint = new APIGatewayProxyFunctionEntryPoint();
                        Func<APIGatewayProxyRequest, ILambdaContext, Task<APIGatewayProxyResponse>> handlerFunc = entryPoint.FunctionHandlerAsync;

                        return HandlerWrapper.GetHandlerWrapper(handlerFunc, new DefaultLambdaJsonSerializer());
                    }
                case "ApplicationLoadBalancerFunctionEntryPoint":
                    {
                        var entryPoint = new ApplicationLoadBalancerFunctionEntryPoint();
                        Func<ApplicationLoadBalancerRequest, ILambdaContext, Task<ApplicationLoadBalancerResponse>> handlerFunc = entryPoint.FunctionHandlerAsync;

                        return HandlerWrapper.GetHandlerWrapper(handlerFunc, new DefaultLambdaJsonSerializer());
                    }
                case "APIGatewayHttpApiV2ProxyFunctionEntryPoint":
                    {
                        var entryPoint = new APIGatewayHttpApiV2ProxyFunctionEntryPoint();
                        Func<APIGatewayHttpApiV2ProxyRequest, ILambdaContext, Task<APIGatewayHttpApiV2ProxyResponse>> handlerFunc = entryPoint.FunctionHandlerAsync;

                        return HandlerWrapper.GetHandlerWrapper(handlerFunc, new DefaultLambdaJsonSerializer());
                    }
                default:
                    throw new ArgumentException("Handler not found.");
            }
        }
    }
}
