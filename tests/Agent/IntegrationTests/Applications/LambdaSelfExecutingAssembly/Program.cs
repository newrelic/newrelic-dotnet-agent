// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.Lambda.CloudWatchEvents.ScheduledEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.Lambda.KinesisEvents;
using Amazon.Lambda.KinesisFirehoseEvents;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.S3Events;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SimpleEmailEvents;
using Amazon.Lambda.SimpleEmailEvents.Actions;
using Amazon.Lambda.SNSEvents;
using Amazon.Lambda.SQSEvents;
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
                case nameof(S3EventHandler):
                    return HandlerWrapper.GetHandlerWrapper<S3Event>(S3EventHandler, serializer);
                case nameof(S3EventHandlerAsync):
                    return HandlerWrapper.GetHandlerWrapper<S3Event>(S3EventHandlerAsync, serializer);
                case nameof(SesEventHandler):
                    return HandlerWrapper.GetHandlerWrapper<SimpleEmailEvent<LambdaReceiptAction>>(SesEventHandler, serializer);
                case nameof(SesEventHandlerAsync):
                    return HandlerWrapper.GetHandlerWrapper<SimpleEmailEvent<LambdaReceiptAction>>(SesEventHandlerAsync, serializer);
                case nameof(DynamoDbEventHandler):
                    return HandlerWrapper.GetHandlerWrapper<DynamoDBEvent>(DynamoDbEventHandler, serializer);
                case nameof(DynamoDbEventHandlerAsync):
                    return HandlerWrapper.GetHandlerWrapper<DynamoDBEvent>(DynamoDbEventHandlerAsync, serializer);
                case nameof(DynamoDbTimeWindowEventHandler):
                    return HandlerWrapper.GetHandlerWrapper<DynamoDBTimeWindowEvent>(DynamoDbTimeWindowEventHandler, serializer);
                case nameof(DynamoDbTimeWindowEventHandlerAsync):
                    return HandlerWrapper.GetHandlerWrapper<DynamoDBTimeWindowEvent>(DynamoDbTimeWindowEventHandlerAsync, serializer);
                case nameof(ApiGatewayProxyRequestHandler):
                    return HandlerWrapper.GetHandlerWrapper<APIGatewayProxyRequest, APIGatewayProxyResponse>(ApiGatewayProxyRequestHandler, serializer);
                case nameof(ApiGatewayProxyRequestHandlerAsync):
                    return HandlerWrapper.GetHandlerWrapper<APIGatewayProxyRequest, APIGatewayProxyResponse>(ApiGatewayProxyRequestHandlerAsync, serializer);
                case nameof (ApiGatewayProxyRequestHandlerReturnsStream):
                    return HandlerWrapper.GetHandlerWrapper<APIGatewayProxyRequest, Stream>(ApiGatewayProxyRequestHandlerReturnsStream, serializer);
                case nameof (ApiGatewayProxyRequestHandlerReturnsStreamAsync):
                    return HandlerWrapper.GetHandlerWrapper<APIGatewayProxyRequest, Stream>(ApiGatewayProxyRequestHandlerReturnsStreamAsync, serializer);
                case nameof(ApiGatewayHttpApiV2ProxyRequestHandler):
                    return HandlerWrapper.GetHandlerWrapper<APIGatewayHttpApiV2ProxyRequest, APIGatewayHttpApiV2ProxyResponse>(ApiGatewayHttpApiV2ProxyRequestHandler, serializer);
                case nameof(ApiGatewayHttpApiV2ProxyRequestHandlerAsync):
                    return HandlerWrapper.GetHandlerWrapper<APIGatewayHttpApiV2ProxyRequest, APIGatewayHttpApiV2ProxyResponse>(ApiGatewayHttpApiV2ProxyRequestHandlerAsync, serializer);
                case nameof(ApplicationLoadBalancerRequestHandler):
                    return HandlerWrapper.GetHandlerWrapper<ApplicationLoadBalancerRequest, ApplicationLoadBalancerResponse>(ApplicationLoadBalancerRequestHandler, serializer);
                case nameof(ApplicationLoadBalancerRequestHandlerAsync):
                    return HandlerWrapper.GetHandlerWrapper<ApplicationLoadBalancerRequest, ApplicationLoadBalancerResponse>(ApplicationLoadBalancerRequestHandlerAsync, serializer);
                case nameof(ApplicationLoadBalancerRequestHandlerReturnsStream):
                    return HandlerWrapper.GetHandlerWrapper<ApplicationLoadBalancerRequest, Stream>(ApplicationLoadBalancerRequestHandlerReturnsStream, serializer);
                case nameof(ApplicationLoadBalancerRequestHandlerReturnsStreamAsync):
                    return HandlerWrapper.GetHandlerWrapper<ApplicationLoadBalancerRequest, Stream>(ApplicationLoadBalancerRequestHandlerReturnsStreamAsync, serializer);
                case nameof(ScheduledCloudWatchEventHandler):
                    return HandlerWrapper.GetHandlerWrapper<ScheduledEvent>(ScheduledCloudWatchEventHandler, serializer);
                case nameof(ScheduledCloudWatchEventHandlerAsync):
                    return HandlerWrapper.GetHandlerWrapper<ScheduledEvent>(ScheduledCloudWatchEventHandlerAsync, serializer);
                case nameof(KinesisFirehoseEventHandler):
                    return HandlerWrapper.GetHandlerWrapper<KinesisFirehoseEvent, KinesisFirehoseResponse>(KinesisFirehoseEventHandler, serializer);
                case nameof(KinesisFirehoseEventHandlerAsync):
                    return HandlerWrapper.GetHandlerWrapper<KinesisFirehoseEvent, KinesisFirehoseResponse>(KinesisFirehoseEventHandlerAsync, serializer);
                case nameof(KinesisEventHandler):
                    return HandlerWrapper.GetHandlerWrapper<KinesisEvent>(KinesisEventHandler, serializer);
                case nameof(KinesisEventHandlerAsync):
                    return HandlerWrapper.GetHandlerWrapper<KinesisEvent>(KinesisEventHandlerAsync, serializer);
                case nameof(KinesisTimeWindowEventHandler):
                    return HandlerWrapper.GetHandlerWrapper<KinesisTimeWindowEvent>(KinesisTimeWindowEventHandler, serializer);
                case nameof(KinesisTimeWindowEventHandlerAsync):
                    return HandlerWrapper.GetHandlerWrapper<KinesisTimeWindowEvent>(KinesisTimeWindowEventHandlerAsync, serializer);
                case nameof(SqsHandler):
                    return HandlerWrapper.GetHandlerWrapper<SQSEvent>(SqsHandler, serializer);
                case nameof(SqsHandlerAsync):
                    return HandlerWrapper.GetHandlerWrapper<SQSEvent>(SqsHandlerAsync, serializer);
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

        public static void S3EventHandler(S3Event _, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(S3EventHandler));
        }

        public static async Task S3EventHandlerAsync(S3Event _, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(S3EventHandlerAsync));
            await Task.Delay(100);
        }

        public static void SesEventHandler(SimpleEmailEvent<LambdaReceiptAction> _, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(SesEventHandler));
        }

        public static async Task SesEventHandlerAsync(SimpleEmailEvent<LambdaReceiptAction> _, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(SesEventHandler));
            await Task.Delay(100);
        }

        public static void DynamoDbEventHandler(DynamoDBEvent _, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(DynamoDbEventHandler));
        }

        public static async Task DynamoDbEventHandlerAsync(DynamoDBEvent _, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(DynamoDbEventHandlerAsync));
            await Task.Delay(100);
        }

        public static void DynamoDbTimeWindowEventHandler(DynamoDBTimeWindowEvent _, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(DynamoDbTimeWindowEventHandler));
        }

        public static async Task DynamoDbTimeWindowEventHandlerAsync(DynamoDBTimeWindowEvent _, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(DynamoDbTimeWindowEventHandler));
            await Task.Delay(100);
        }

        public static APIGatewayProxyResponse ApiGatewayProxyRequestHandler(APIGatewayProxyRequest apiGatewayProxyRequest, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(ApiGatewayProxyRequestHandler));

            return new APIGatewayProxyResponse() { Body = apiGatewayProxyRequest.Body, StatusCode = 200, Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Content-Length", "12345" } } };
        }

        public static async Task<APIGatewayProxyResponse> ApiGatewayProxyRequestHandlerAsync(APIGatewayProxyRequest apiGatewayProxyRequest, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(ApiGatewayProxyRequestHandlerAsync));
            await Task.Delay(100);

            return new APIGatewayProxyResponse() { Body = apiGatewayProxyRequest.Body, StatusCode = 200, Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Content-Length", "12345" } } };
        }

        public static Stream ApiGatewayProxyRequestHandlerReturnsStream(APIGatewayProxyRequest apiGatewayProxyRequest, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(ApiGatewayProxyRequestHandlerReturnsStream));

            var serializer = new DefaultLambdaJsonSerializer();

            var response = new APIGatewayProxyResponse() { Body = apiGatewayProxyRequest.Body, StatusCode = 200, Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Content-Length", "12345" } } };
            var stream = new MemoryStream();
            serializer.Serialize(response, stream);
            return stream;
        }

        public static async Task<Stream> ApiGatewayProxyRequestHandlerReturnsStreamAsync(APIGatewayProxyRequest apiGatewayProxyRequest, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(ApiGatewayProxyRequestHandlerReturnsStreamAsync));

            await Task.Delay(100);

            var serializer = new DefaultLambdaJsonSerializer();

            var response = new APIGatewayProxyResponse() { Body = apiGatewayProxyRequest.Body, StatusCode = 200, Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Content-Length", "12345" } } };
            var stream = new MemoryStream();
            serializer.Serialize(response, stream);

            return stream;
        }

        public static APIGatewayHttpApiV2ProxyResponse ApiGatewayHttpApiV2ProxyRequestHandler(APIGatewayHttpApiV2ProxyRequest apiGatewayProxyRequest, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(ApiGatewayHttpApiV2ProxyRequestHandler));

            return new APIGatewayHttpApiV2ProxyResponse() { Body = apiGatewayProxyRequest.Body, StatusCode = 200, Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Content-Length", "12345" } } };
        }

        public static async Task<APIGatewayHttpApiV2ProxyResponse> ApiGatewayHttpApiV2ProxyRequestHandlerAsync(APIGatewayHttpApiV2ProxyRequest apiGatewayProxyRequest, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(ApiGatewayHttpApiV2ProxyRequestHandlerAsync));
            await Task.Delay(100);

            return new APIGatewayHttpApiV2ProxyResponse() { Body = apiGatewayProxyRequest.Body, StatusCode = 200, Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Content-Length", "12345" } } };
        }

        public static ApplicationLoadBalancerResponse ApplicationLoadBalancerRequestHandler(ApplicationLoadBalancerRequest applicationLoadBalancerRequest, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(ApplicationLoadBalancerRequestHandler));

            return new ApplicationLoadBalancerResponse() { Body = applicationLoadBalancerRequest.Body, StatusCode = 200, Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Content-Length", "12345" } } };
        }

        public static async Task<ApplicationLoadBalancerResponse> ApplicationLoadBalancerRequestHandlerAsync(ApplicationLoadBalancerRequest applicationLoadBalancerRequest, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(ApplicationLoadBalancerRequestHandlerAsync));
            await Task.Delay(100);

            return new ApplicationLoadBalancerResponse() { Body = applicationLoadBalancerRequest.Body, StatusCode = 200, Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Content-Length", "12345" } } };
        }

        public static Stream ApplicationLoadBalancerRequestHandlerReturnsStream(ApplicationLoadBalancerRequest applicationLoadBalancerRequest, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(ApplicationLoadBalancerRequestHandlerReturnsStream));

            var serializer = new DefaultLambdaJsonSerializer();
            var response = new ApplicationLoadBalancerResponse() { Body = applicationLoadBalancerRequest.Body, StatusCode = 200, Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Content-Length", "12345" } } };
            var stream = new MemoryStream();
            serializer.Serialize(response, stream);

            return stream;
        }

        public static async Task<Stream> ApplicationLoadBalancerRequestHandlerReturnsStreamAsync(ApplicationLoadBalancerRequest applicationLoadBalancerRequest, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(ApplicationLoadBalancerRequestHandlerReturnsStreamAsync));
            await Task.Delay(100);

            var serializer = new DefaultLambdaJsonSerializer();
            var response = new ApplicationLoadBalancerResponse() { Body = applicationLoadBalancerRequest.Body, StatusCode = 200, Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Content-Length", "12345" } } };
            var stream = new MemoryStream();
            serializer.Serialize(response, stream);
            return stream;
        }

        public static void ScheduledCloudWatchEventHandler(ScheduledEvent _, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(ScheduledCloudWatchEventHandler));
        }

        public static async Task ScheduledCloudWatchEventHandlerAsync(ScheduledEvent _, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(ScheduledCloudWatchEventHandlerAsync));
            await Task.Delay(100);
        }

        public static KinesisFirehoseResponse KinesisFirehoseEventHandler(KinesisFirehoseEvent evnt,  ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(KinesisFirehoseEventHandler));

            var response = new KinesisFirehoseResponse
            {
                Records = new List<KinesisFirehoseResponse.FirehoseRecord>()
            };

            foreach (var record in evnt.Records)
            {
                var transformedRecord = new KinesisFirehoseResponse.FirehoseRecord
                {
                    RecordId = record.RecordId,
                    Result = KinesisFirehoseResponse.TRANSFORMED_STATE_OK
                };
                transformedRecord.EncodeData(record.DecodeData().ToUpperInvariant());

                response.Records.Add(transformedRecord);
            }

            return response;
        }

        public static async Task<KinesisFirehoseResponse> KinesisFirehoseEventHandlerAsync(KinesisFirehoseEvent evnt, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(KinesisFirehoseEventHandlerAsync));

            var response = new KinesisFirehoseResponse
            {
                Records = new List<KinesisFirehoseResponse.FirehoseRecord>()
            };

            foreach (var record in evnt.Records)
            {
                var transformedRecord = new KinesisFirehoseResponse.FirehoseRecord
                {
                    RecordId = record.RecordId,
                    Result = KinesisFirehoseResponse.TRANSFORMED_STATE_OK
                };
                transformedRecord.EncodeData(record.DecodeData().ToUpperInvariant());

                response.Records.Add(transformedRecord);
            }

            return await Task.FromResult(response);
        }

        public static void KinesisEventHandler(KinesisEvent _,  ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(KinesisEventHandler));
        }

        public static async Task KinesisEventHandlerAsync(KinesisEvent _, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(KinesisEventHandlerAsync));
            await Task.Delay(100);
        }

        public static void KinesisTimeWindowEventHandler(KinesisTimeWindowEvent _, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(KinesisEventHandler));
        }

        public static async Task KinesisTimeWindowEventHandlerAsync(KinesisTimeWindowEvent _, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(KinesisEventHandlerAsync));
            await Task.Delay(100);
        }

        public static void SqsHandler(SQSEvent _, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(SqsHandler));
        }

        public static async Task SqsHandlerAsync(SQSEvent _, ILambdaContext __)
        {
            Console.WriteLine("Executing lambda {0}", nameof(SqsHandlerAsync));
            await Task.Delay(100);
        }
    }
}
