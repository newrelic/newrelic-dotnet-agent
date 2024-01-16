// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Amazon.SQS.Model;
using Amazon.SQS;
using OpenTracing.Mock;
using OpenTracing.Util;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.OpenTracing.AmazonLambda.State;
using NewRelic.OpenTracing.AmazonLambda.Util;
using NewRelic.OpenTracing.AmazonLambda.DiagnosticObserver;
using Logger = NewRelic.OpenTracing.AmazonLambda.Util.Logger;
using Amazon.SimpleNotificationService.Model;
using Amazon.SimpleNotificationService;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace NewRelic.Tests.AwsLambda.AwsLambdaWrapperTests
{
    [TestFixture]
    public class LambdaWrapperTests
    {
        private const string TestArn = "test-arn";

        private IDictionary<string, string> _singleValueHeaders;
        private MockTracer _tracer;
        private LambdaSpan _span;
        private IDictionary<string, string> _tags;

        private ILogger _logger = new Logger();
        private IFileSystemManager _fileSystemManager = new FileSystemManager();

        [SetUp]
        public void Setup()
        {
            _span = new LambdaRootSpan("testOperation", new DateTimeOffset(), new Dictionary<string, object>(), "guid", new DataCollector(_logger, false, _fileSystemManager), new TransactionState(), new PrioritySamplingState(), new DistributedTracingState());
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _tracer = new MockTracer();
            if (!GlobalTracer.IsRegistered())
                GlobalTracer.Register(_tracer);

            _singleValueHeaders = new Dictionary<string, string>
            {
                { "X-Forwarded-Port","1234" },
                { "X-Forwarded-Proto","proto" },
                { "Content-Type","application/json" },
                { "Content-Length","1000" },
                { "Access-Control-Allow-Origin", "*" },
                { "Custom-Header","CustomValue" },
                { "newrelic","dt-payload" },
            };

            _tags = new Dictionary<string, string>
            {
                { "testTag","1234" },
            };

            AwsServiceHandler.UseDTWrapperValueFactory = new Func<bool>(() => true);
        }

        [TearDown]
        public void TearDown()
        {
            _tracer.Reset();
        }

        #region APIGatewayProxy

        [Test]
        public void LambdaWrapper_ForAPIGatewayProxyWithoutInput()
        {
            var context = new TestLambdaContext
            {
                AwsRequestId = "testId",
                InvokedFunctionArn = TestArn
            };

            var wrappedHandler = new TracingRequestHandler().LambdaWrapper(APIGatewayProxyFunctionHandlerWithoutInput, context);
            var span = _tracer.FinishedSpans()[0];

            Assert.That((string)span.Tags["aws.requestId"], Is.EqualTo("testId"));
            Assert.That((string)span.Tags["aws.arn"], Is.EqualTo(TestArn));
            Assert.That((string)span.Tags["response.status"], Is.EqualTo("200"));
        }

        [Test]
        public void LambdaWrapper_ForAPIGatewayProxyWithInput()
        {
            var request = new APIGatewayProxyRequest
            {
                HttpMethod = "POST",
                Path = "/test/path",
                Headers = _singleValueHeaders
            };

            var context = new TestLambdaContext
            {
                AwsRequestId = "testId",
                InvokedFunctionArn = TestArn
            };

            var wrappedHandler = new TracingRequestHandler().LambdaWrapper(APIGatewayProxyFunctionHandlerWithInput, request, context);
            var span = _tracer.FinishedSpans()[0];

            Assert.That((string)span.Tags["aws.requestId"], Is.EqualTo("testId"));
            Assert.That((string)span.Tags["aws.arn"], Is.EqualTo(TestArn));
            Assert.That((string)span.Tags["response.status"], Is.EqualTo("200"));
            Assert.That(span.Tags.ContainsKey("newrelic"), Is.False);
        }

        [Test]
        public async Task LambdaWrapper_ForAPIGatewayProxyWithInputAsync()
        {
            var request = new APIGatewayProxyRequest
            {
                HttpMethod = "POST",
                Path = "/test/path",
                Headers = _singleValueHeaders
            };

            var context = new TestLambdaContext
            {
                AwsRequestId = "testId",
                InvokedFunctionArn = TestArn
            };

            var wrappedHandler = await new TracingRequestHandler().LambdaWrapper(APIGatewayProxyFunctionHandlerWithInputAsync, request, context);
            var span = _tracer.FinishedSpans()[0];

            Assert.That((string)span.Tags["aws.requestId"], Is.EqualTo("testId"));
            Assert.That((string)span.Tags["aws.arn"], Is.EqualTo(TestArn));
            Assert.That((string)span.Tags["response.status"], Is.EqualTo("200"));
            Assert.That(span.Tags.ContainsKey("newrelic"), Is.False);
        }

        #endregion

        #region ApplicationLoadBalancer

        [Test]
        public void LambdaWrapper_ForApplicationLoadBalancerWithoutInput()
        {
            var context = new TestLambdaContext
            {
                AwsRequestId = "testId",
                InvokedFunctionArn = TestArn
            };

            var wrappedHandler = new TracingRequestHandler().LambdaWrapper(ApplicationLoadBalancerFunctionHandlerWithoutInput, context);
            var span = _tracer.FinishedSpans()[0];

            Assert.That((string)span.Tags["aws.requestId"], Is.EqualTo("testId"));
            Assert.That((string)span.Tags["aws.arn"], Is.EqualTo(TestArn));
            Assert.That((string)span.Tags["response.status"], Is.EqualTo("200"));
        }

        [Test]
        public async Task LambdaWrapper_ForApplicationLoadBalancerWithoutInputAsync()
        {
            var context = new TestLambdaContext
            {
                AwsRequestId = "testId",
                InvokedFunctionArn = TestArn
            };

            var wrappedHandler = await new TracingRequestHandler().LambdaWrapper(ApplicationLoadBalancerFunctionHandlerWithoutInputAsync, context);
            var span = _tracer.FinishedSpans()[0];

            Assert.That((string)span.Tags["aws.requestId"], Is.EqualTo("testId"));
            Assert.That((string)span.Tags["aws.arn"], Is.EqualTo(TestArn));
            Assert.That((string)span.Tags["response.status"], Is.EqualTo("200"));
        }

        [Test]
        public void LambdaWrapper_ForApplicationLoadBalancerWithInput()
        {
            var albRequestContext = new ApplicationLoadBalancerRequest.ALBRequestContext
            {
                Elb = new ApplicationLoadBalancerRequest.ElbInfo()
            };
            albRequestContext.Elb.TargetGroupArn = TestArn;
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Path = "/test/path",
                Headers = _singleValueHeaders,
                RequestContext = albRequestContext
            };

            var context = new TestLambdaContext
            {
                AwsRequestId = "testId",
                InvokedFunctionArn = TestArn
            };

            var wrappedHandler = new TracingRequestHandler().LambdaWrapper(ApplicationLoadBalancerFunctionHandlerWithInput, request, context);
            var span = _tracer.FinishedSpans()[0];

            Assert.That((string)span.Tags["aws.requestId"], Is.EqualTo("testId"));
            Assert.That((string)span.Tags["aws.arn"], Is.EqualTo(TestArn));
            Assert.That((string)span.Tags["response.status"], Is.EqualTo("200"));
            Assert.That(span.Tags.ContainsKey("newrelic"), Is.False);
        }

        #endregion

        #region Dictionary and Void

        [Test]
        public void LambdaWrapper_ForIDictionaryWithoutInput()
        {
            var context = new TestLambdaContext
            {
                AwsRequestId = "testId",
                InvokedFunctionArn = TestArn
            };

            var wrappedHandler = new TracingRequestHandler().LambdaWrapper(IDictionaryFunctionHandlerWithoutInput, context);
            var span = _tracer.FinishedSpans()[0];

            Assert.That((string)span.Tags["aws.requestId"], Is.EqualTo("testId"));
            Assert.That((string)span.Tags["aws.arn"], Is.EqualTo(TestArn));
            Assert.That((string)span.Tags["response.status"], Is.EqualTo("200"));
        }

        [Test]
        public void LambdaWrapper_ForVoidWithoutInput()
        {
            var context = new TestLambdaContext
            {
                AwsRequestId = "testId",
                InvokedFunctionArn = TestArn
            };

            new TracingRequestHandler().LambdaWrapper(VoidFunctionHandlerWithoutInput, context);
            var span = _tracer.FinishedSpans()[0];

            Assert.That((string)span.Tags["aws.requestId"], Is.EqualTo("testId"));
            Assert.That((string)span.Tags["aws.arn"], Is.EqualTo(TestArn));
            Assert.That(span.Tags.ContainsKey("response.status"), Is.False);
        }

        [Test]
        public async Task LambdaWrapper_ForVoidWithoutInputAsync()
        {
            var context = new TestLambdaContext
            {
                AwsRequestId = "testId",
                InvokedFunctionArn = TestArn
            };

            await new TracingRequestHandler().LambdaWrapper(VoidFunctionHandlerWithoutInputAsync, context);
            var span = _tracer.FinishedSpans()[0];

            Assert.That((string)span.Tags["aws.requestId"], Is.EqualTo("testId"));
            Assert.That((string)span.Tags["aws.arn"], Is.EqualTo(TestArn));
            Assert.That(span.Tags.ContainsKey("response.status"), Is.False);
        }

        [Test]
        public void LambdaWrapper_ForVoidWithInput()
        {
            var context = new TestLambdaContext
            {
                AwsRequestId = "testId",
                InvokedFunctionArn = TestArn
            };

            var inputString = string.Empty;
            new TracingRequestHandler().LambdaWrapper(VoidFunctionHandlerWithInput, inputString, context);
            var span = _tracer.FinishedSpans()[0];

            Assert.That((string)span.Tags["aws.requestId"], Is.EqualTo("testId"));
            Assert.That((string)span.Tags["aws.arn"], Is.EqualTo(TestArn));
            Assert.That(span.Tags.ContainsKey("response.status"), Is.False);
        }

        #endregion

        [Test]
        public void LambdaWrapper_ErrorHandling()
        {
            var context = new TestLambdaContext
            {
                AwsRequestId = "testId",
                InvokedFunctionArn = TestArn
            };

            try
            {
                new TracingRequestHandler().LambdaWrapper(ThrowException, context);
            }
            catch
            {
                var span = _tracer.FinishedSpans()[0];
                Assert.That((string)span.Tags["aws.requestId"], Is.EqualTo("testId"));
                Assert.That((string)span.Tags["aws.arn"], Is.EqualTo(TestArn));

                Assert.That(span.LogEntries.Count, Is.EqualTo(1));
                Assert.That((string)span.LogEntries[0].Fields["event"], Is.EqualTo("error"));
                Assert.That(span.LogEntries[0].Fields["error.object"], Is.TypeOf(typeof(System.Exception)));
                Assert.That((string)span.LogEntries[0].Fields["message"], Is.EqualTo("my exception"));
                Assert.That((string)span.LogEntries[0].Fields["error.kind"], Is.EqualTo("Exception"));
                ClassicAssert.IsTrue(((string)span.LogEntries[0].Fields["stack"]).Contains("NewRelic.Tests.AwsLambda.AwsLambdaWrapperTests.LambdaWrapperTests.ThrowException(ILambdaContext context)"));
                return;
            }
            Assert.Fail("Did not catch exception as expected.");
        }

        [Test]
        public async Task LambdaWrapper_ErrorHandlingAsync()
        {
            var context = new TestLambdaContext
            {
                AwsRequestId = "testId",
                InvokedFunctionArn = TestArn
            };

            try
            {
                await new TracingRequestHandler().LambdaWrapper(ThrowExceptionAsync, context);
            }
            catch
            {
                var span = _tracer.FinishedSpans()[0];
                Assert.That((string)span.Tags["aws.requestId"], Is.EqualTo("testId"));
                Assert.That((string)span.Tags["aws.arn"], Is.EqualTo(TestArn));

                Assert.That(span.LogEntries.Count, Is.EqualTo(1));
                Assert.That((string)span.LogEntries[0].Fields["event"], Is.EqualTo("error"));
                Assert.That(span.LogEntries[0].Fields["error.object"], Is.TypeOf(typeof(System.Exception)));
                Assert.That((string)span.LogEntries[0].Fields["message"], Is.EqualTo("my exception"));
                Assert.That((string)span.LogEntries[0].Fields["error.kind"], Is.EqualTo("Exception"));
                ClassicAssert.IsTrue(((string)span.LogEntries[0].Fields["stack"]).Contains("NewRelic.Tests.AwsLambda.AwsLambdaWrapperTests.LambdaWrapperTests.ThrowExceptionAsync(ILambdaContext context)"));
                return;
            }
            Assert.Fail("Did not catch exception as expected.");
        }

        #region SQS

        [Test]
        public async Task SQSWrapper_SendMessageRequest_WrapRequestWithObject_OkResponse()
        {
            const string TestQueueName = "testQueueName";
            const string ExpectedOperationName = "MessageBroker/SQS/Queue/Produce/Named/" + TestQueueName;
            const string QueueUrl = "https://sqs.us-west-2.amazonaws.com/123456789/" + TestQueueName;
            var sendMessageRequest = new SendMessageRequest(QueueUrl, "myMessage");

            var result = await SQSWrapper.WrapRequest(SendSQSMessage, sendMessageRequest);

            var span = _tracer.FinishedSpans()[0];
            Assert.That(span.Tags["span.kind"], Is.EqualTo("client"));
            Assert.That(span.Tags["component"], Is.EqualTo("SQS"));
            Assert.That(span.Tags["aws.operation"], Is.EqualTo("Produce"));
            Assert.That(span.Tags["http.status_code"], Is.EqualTo((int)HttpStatusCode.OK));
            Assert.That(span.OperationName, Is.EqualTo(ExpectedOperationName));
        }

        [Test]
        public async Task SQSWrapper_SendMessageRequest_WrapRequestWithQueueAndMessage_BadResponse()
        {
            const string TestQueueName = "testQueueName";
            const string ExpectedOperationName = "MessageBroker/SQS/Queue/Produce/Named/" + TestQueueName;
            const string QueueUrl = "https://sqs.us-west-2.amazonaws.com/123456789/" + TestQueueName;

            var result = await SQSWrapper.WrapRequest(SendSQSMessageWithBadResult, QueueUrl, "myMessage");

            var span = _tracer.FinishedSpans()[0];
            Assert.That(span.Tags["span.kind"], Is.EqualTo("client"));
            Assert.That(span.Tags["component"], Is.EqualTo("SQS"));
            Assert.That(span.Tags["aws.operation"], Is.EqualTo("Produce"));
            Assert.That(span.Tags["http.status_code"], Is.EqualTo((int)HttpStatusCode.BadRequest));
            Assert.That(span.OperationName, Is.EqualTo(ExpectedOperationName));
        }

        [Test]
        public async Task SQSWrapper_SendMessageRequest_WrapRequestWithObject_GeneratesException()
        {
            const string TestQueueName = "testQueueName";
            const string ExpectedOperationName = "MessageBroker/SQS/Queue/Produce/Named/" + TestQueueName;
            const string QueueUrl = "https://sqs.us-west-2.amazonaws.com/123456789/" + TestQueueName;
            const string ExpectedExceptionMessage = "whoopsie";
            var sendMessageRequest = new SendMessageRequest(QueueUrl, ExpectedExceptionMessage);

            try
            {
                var result = await SQSWrapper.WrapRequest(SendSQSMessageWithException, sendMessageRequest);
            }
            catch (Exception)
            {
                var span = _tracer.FinishedSpans()[0];
                Assert.That(span.Tags["span.kind"], Is.EqualTo("client"));
                Assert.That(span.Tags["component"], Is.EqualTo("SQS"));
                Assert.That(span.Tags["aws.operation"], Is.EqualTo("Produce"));
                Assert.That(span.Tags["error"], Is.EqualTo(true));

                Assert.That(span.LogEntries.Count, Is.EqualTo(1));
                Assert.That((string)span.LogEntries[0].Fields["event"], Is.EqualTo("error"));
                Assert.That(span.LogEntries[0].Fields["error.object"], Is.TypeOf(typeof(AmazonSQSException)));
                Assert.That((string)span.LogEntries[0].Fields["message"], Is.EqualTo(ExpectedExceptionMessage));
                Assert.That((string)span.LogEntries[0].Fields["error.kind"], Is.EqualTo("Exception"));
                ClassicAssert.IsTrue(((string)span.LogEntries[0].Fields["stack"]).Contains("NewRelic.Tests.AwsLambda.AwsLambdaWrapperTests.LambdaWrapperTests.SendSQSMessageWithException(SendMessageRequest sendMessageRequest, CancellationToken cancellationToken)"));

                Assert.That(span.OperationName, Is.EqualTo(ExpectedOperationName));
                return;
            }

            Assert.Fail("Did not catch exception as expected.");
        }

        [Test]
        public async Task SQSWrapper_SendMessageBatchRequest_WrapRequestWithObject_OkResponse()
        {
            const string TestQueueName = "testQueueName";
            const string ExpectedOperationName = "MessageBroker/SQS/Queue/Produce/Named/" + TestQueueName;
            const string QueueUrl = "https://sqs.us-west-2.amazonaws.com/123456789/" + TestQueueName;
            var batchEntries = new List<SendMessageBatchRequestEntry>();
            batchEntries.Add(new SendMessageBatchRequestEntry("req1", "batch1message"));
            var sendMessageBatchRequest = new SendMessageBatchRequest(QueueUrl, batchEntries);

            var result = await SQSWrapper.WrapRequest(SendSQSBatchMessage, sendMessageBatchRequest);

            var span = _tracer.FinishedSpans()[0];
            Assert.That(span.Tags["span.kind"], Is.EqualTo("client"));
            Assert.That(span.Tags["component"], Is.EqualTo("SQS"));
            Assert.That(span.Tags["aws.operation"], Is.EqualTo("Produce"));
            Assert.That(span.Tags["http.status_code"], Is.EqualTo((int)HttpStatusCode.OK));
            Assert.That(span.OperationName, Is.EqualTo(ExpectedOperationName));
        }

        [Test]
        public async Task SQSWrapper_SendMessageBatchRequest_WrapRequestWithQueueAndMessage_BadResponse()
        {
            const string TestQueueName = "testQueueName";
            const string ExpectedOperationName = "MessageBroker/SQS/Queue/Produce/Named/" + TestQueueName;
            const string QueueUrl = "https://sqs.us-west-2.amazonaws.com/123456789/" + TestQueueName;
            var batchEntries = new List<SendMessageBatchRequestEntry>();
            batchEntries.Add(new SendMessageBatchRequestEntry("req1", "batch1message"));

            var result = await SQSWrapper.WrapRequest(SendSQSBatchMessageWithBadResult, QueueUrl, batchEntries);

            var span = _tracer.FinishedSpans()[0];
            Assert.That(span.Tags["span.kind"], Is.EqualTo("client"));
            Assert.That(span.Tags["component"], Is.EqualTo("SQS"));
            Assert.That(span.Tags["aws.operation"], Is.EqualTo("Produce"));
            Assert.That(span.Tags["http.status_code"], Is.EqualTo((int)HttpStatusCode.BadRequest));
            Assert.That(span.OperationName, Is.EqualTo(ExpectedOperationName));
        }

        [Test]
        public async Task SQSWrapper_SendMessageBatchRequest_WrapRequestWithObject_GeneratesException()
        {
            const string TestQueueName = "testQueueName";
            const string ExpectedOperationName = "MessageBroker/SQS/Queue/Produce/Named/" + TestQueueName;
            const string QueueUrl = "https://sqs.us-west-2.amazonaws.com/123456789/" + TestQueueName;
            const string ExpectedExceptionMessage = "whoopsie";
            var batchEntries = new List<SendMessageBatchRequestEntry>();
            batchEntries.Add(new SendMessageBatchRequestEntry("req1", ExpectedExceptionMessage));
            var sendMessageBatchRequest = new SendMessageBatchRequest(QueueUrl, batchEntries);

            try
            {
                var result = await SQSWrapper.WrapRequest(SendSQSBatchMessageWithException, sendMessageBatchRequest);
            }
            catch (Exception)
            {
                var span = _tracer.FinishedSpans()[0];
                Assert.That(span.Tags["span.kind"], Is.EqualTo("client"));
                Assert.That(span.Tags["component"], Is.EqualTo("SQS"));
                Assert.That(span.Tags["aws.operation"], Is.EqualTo("Produce"));
                Assert.That(span.Tags["error"], Is.EqualTo(true));

                Assert.That(span.LogEntries.Count, Is.EqualTo(1));
                Assert.That((string)span.LogEntries[0].Fields["event"], Is.EqualTo("error"));
                Assert.That(span.LogEntries[0].Fields["error.object"], Is.TypeOf(typeof(AmazonSQSException)));
                Assert.That((string)span.LogEntries[0].Fields["message"], Is.EqualTo(ExpectedExceptionMessage));
                Assert.That((string)span.LogEntries[0].Fields["error.kind"], Is.EqualTo("Exception"));
                ClassicAssert.IsTrue(((string)span.LogEntries[0].Fields["stack"]).Contains("NewRelic.Tests.AwsLambda.AwsLambdaWrapperTests.LambdaWrapperTests.SendSQSBatchMessageWithException(SendMessageBatchRequest sendMessageBatchRequest, CancellationToken cancellationToken)"));

                Assert.That(span.OperationName, Is.EqualTo(ExpectedOperationName));
                return;
            }

            Assert.Fail("Did not catch exception as expected.");
        }

        #endregion

        #region SNS

        [Test]
        public async Task SNSWrapper_PublishRequest_WrapRequestWithObject_OkResponse()
        {
            const string TestTopicName = "testTopicName";
            const string ExpectedOperationName = "MessageBroker/SNS/Topic/Produce/Named/" + TestTopicName;
            const string TopicArn = "arn:aws:sns:us-west-2:1234567890:" + TestTopicName;
            var publishRequest = new PublishRequest(TopicArn, "myMessage");

            var result = await SNSWrapper.WrapRequest(SendSNSMessage, publishRequest);

            var span = _tracer.FinishedSpans()[0];
            Assert.That(span.Tags["span.kind"], Is.EqualTo("client"));
            Assert.That(span.Tags["component"], Is.EqualTo("SNS"));
            Assert.That(span.Tags["aws.operation"], Is.EqualTo("Produce"));
            Assert.That(span.Tags["http.status_code"], Is.EqualTo((int)HttpStatusCode.OK));
            Assert.That(span.OperationName, Is.EqualTo(ExpectedOperationName));
        }

        [Test]
        public async Task SNSWrapper_PublishRequest_WrapRequestWithTopicAndMessage_BadResponse()
        {
            const string TestTopicName = "testTopicName";
            const string ExpectedOperationName = "MessageBroker/SNS/Topic/Produce/Named/" + TestTopicName;
            const string TopicArn = "arn:aws:sns:us-west-2:1234567890:" + TestTopicName;

            var result = await SNSWrapper.WrapRequest(SendSNSMessageWithBadResult, TopicArn, "myMessage");

            var span = _tracer.FinishedSpans()[0];
            Assert.That(span.Tags["span.kind"], Is.EqualTo("client"));
            Assert.That(span.Tags["component"], Is.EqualTo("SNS"));
            Assert.That(span.Tags["aws.operation"], Is.EqualTo("Produce"));
            Assert.That(span.Tags["http.status_code"], Is.EqualTo((int)HttpStatusCode.BadRequest));
            Assert.That(span.OperationName, Is.EqualTo(ExpectedOperationName));
        }

        [Test]
        public async Task SNSWrapper_PublishRequest_WrapRequestWithObject_GeneratesException()
        {
            const string TestTopicName = "testTopicName";
            const string ExpectedOperationName = "MessageBroker/SNS/Topic/Produce/Named/" + TestTopicName;
            const string TopicArn = "arn:aws:sns:us-west-2:1234567890:" + TestTopicName;
            const string ExpectedExceptionMessage = "whoopsie";
            var publishRequest = new PublishRequest(TopicArn, ExpectedExceptionMessage);

            try
            {
                var result = await SNSWrapper.WrapRequest(SendSNSMessageWithException, publishRequest);
            }
            catch (Exception)
            {
                var span = _tracer.FinishedSpans()[0];
                Assert.That(span.Tags["span.kind"], Is.EqualTo("client"));
                Assert.That(span.Tags["component"], Is.EqualTo("SNS"));
                Assert.That(span.Tags["aws.operation"], Is.EqualTo("Produce"));
                Assert.That(span.Tags["error"], Is.EqualTo(true));

                Assert.That(span.LogEntries.Count, Is.EqualTo(1));
                Assert.That((string)span.LogEntries[0].Fields["event"], Is.EqualTo("error"));
                Assert.That(span.LogEntries[0].Fields["error.object"], Is.TypeOf(typeof(AmazonSimpleNotificationServiceException)));
                Assert.That((string)span.LogEntries[0].Fields["message"], Is.EqualTo(ExpectedExceptionMessage));
                Assert.That((string)span.LogEntries[0].Fields["error.kind"], Is.EqualTo("Exception"));
                ClassicAssert.IsTrue(((string)span.LogEntries[0].Fields["stack"]).Contains("NewRelic.Tests.AwsLambda.AwsLambdaWrapperTests.LambdaWrapperTests.SendSNSMessageWithException(PublishRequest publishRequest, CancellationToken cancellationToken)"));

                Assert.That(span.OperationName, Is.EqualTo(ExpectedOperationName));
                return;
            }

            Assert.Fail("Did not catch exception as expected.");
        }

        [Test]
        public async Task SNSWrapper_PublishRequest_WrapRequestWithPhoneNumberObject_OkResponse()
        {
            const string TestPhoneNumber = "+1503555100";
            const string ExpectedOperationName = "MessageBroker/SNS/Topic/Produce/Named/PhoneNumber";
            var publishRequest = new PublishRequest
            {
                PhoneNumber = TestPhoneNumber,
                Message = "myMessage"
            };

            var result = await SNSWrapper.WrapRequest(SendSNSMessage, publishRequest);

            var span = _tracer.FinishedSpans()[0];
            Assert.That(span.Tags["span.kind"], Is.EqualTo("client"));
            Assert.That(span.Tags["component"], Is.EqualTo("SNS"));
            Assert.That(span.Tags["aws.operation"], Is.EqualTo("Produce"));
            Assert.That(span.Tags["http.status_code"], Is.EqualTo((int)HttpStatusCode.OK));
            Assert.That(span.OperationName, Is.EqualTo(ExpectedOperationName));
        }

        #endregion

        #region DetectColdStart

        [Test]
        public void DetectColdStart_FromColdStart()
        {
            using (var scope = _tracer.BuildSpan("tester").StartActive())
            {
                var isColdStart = 1;
                TracingRequestHandler.DetectColdStart(scope, ref isColdStart);
                var mockSpan = (MockSpan)scope.Span;

                Assert.That(isColdStart, Is.EqualTo(0));
                Assert.That((bool)mockSpan.Tags["aws.lambda.coldStart"], Is.EqualTo(true));
            }
        }

        [Test]
        public void DetectColdStart_FromWarmStart()
        {
            using (var scope = _tracer.BuildSpan("tester").StartActive())
            {
                var isColdStart = 0;
                TracingRequestHandler.DetectColdStart(scope, ref isColdStart);
                var mockSpan = (MockSpan)scope.Span;

                Assert.That(isColdStart, Is.EqualTo(0));
                Assert.That(mockSpan.Tags.ContainsKey("aws.lambda.coldStart"), Is.EqualTo(false));
            }
        }

        #endregion

        #region Tags

        [Test]
        public void AddTagsToActiveSpan_NullSpan()
        {
            TracingRequestHandler.AddTagsToActiveSpan(null, "prefix", _tags);
            Assert.That(_span.Tags.Count, Is.EqualTo(0));
        }

        [Test]
        public void AddTagsToActiveSpan_NullPrefix()
        {
            TracingRequestHandler.AddTagsToActiveSpan(_span, null, _tags);
            Assert.That(_span.Tags.Count, Is.EqualTo(0));
        }

        [Test]
        public void AddTagsToActiveSpan_EmptyPrefix()
        {
            TracingRequestHandler.AddTagsToActiveSpan(_span, string.Empty, _tags);
            Assert.That(_span.Tags.Count, Is.EqualTo(0));
        }

        [Test]
        public void AddTagsToActiveSpan_EmptyDictionary()
        {
            TracingRequestHandler.AddTagsToActiveSpan(_span, "prefix", new Dictionary<string, string>());
            Assert.That(_span.Tags.Count, Is.EqualTo(0));
        }

        [Test]
        public void AddTagsToActiveSpan_NullDictionary()
        {
            TracingRequestHandler.AddTagsToActiveSpan(_span, "prefix", null);
            Assert.That(_span.Tags.Count, Is.EqualTo(0));
        }

        [Test]
        public void AddTagsToActiveSpan_AllPopulated()
        {
            TracingRequestHandler.AddTagsToActiveSpan(_span, "prefix", _tags);
            Assert.That((string)_span.GetTag("prefix.testTag"), Is.EqualTo("1234"));
        }

        #endregion

        #region Functions

        private APIGatewayProxyResponse APIGatewayProxyFunctionHandlerWithoutInput(ILambdaContext context)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
            };
        }

        private APIGatewayProxyResponse APIGatewayProxyFunctionHandlerWithInput(APIGatewayProxyRequest input, ILambdaContext context)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Headers = _singleValueHeaders
            };
        }

        private Task<APIGatewayProxyResponse> APIGatewayProxyFunctionHandlerWithInputAsync(APIGatewayProxyRequest input, ILambdaContext context)
        {
            return Task.FromResult(new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Headers = _singleValueHeaders
            });
        }

        private ApplicationLoadBalancerResponse ApplicationLoadBalancerFunctionHandlerWithoutInput(ILambdaContext context)
        {
            return new ApplicationLoadBalancerResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
            };
        }


        private Task<ApplicationLoadBalancerResponse> ApplicationLoadBalancerFunctionHandlerWithoutInputAsync(ILambdaContext context)
        {
            return Task.FromResult(new ApplicationLoadBalancerResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
            });
        }

        private ApplicationLoadBalancerResponse ApplicationLoadBalancerFunctionHandlerWithInput(ApplicationLoadBalancerRequest input, ILambdaContext context)
        {
            return new ApplicationLoadBalancerResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Headers = _singleValueHeaders
            };
        }

        private IDictionary<string, object> IDictionaryFunctionHandlerWithoutInput(ILambdaContext context)
        {
            return new Dictionary<string, object>()
            {
                { "statusCode", 200},
            };
        }

        private void VoidFunctionHandlerWithoutInput(ILambdaContext context)
        {
            // Do Nothing
        }

        private async Task VoidFunctionHandlerWithoutInputAsync(ILambdaContext context)
        {
            await Task.CompletedTask;
        }

        private void VoidFunctionHandlerWithInput(string input, ILambdaContext context)
        {
            // Do Nothing
        }

        private void ThrowException(ILambdaContext context)
        {
            throw new System.Exception("my exception");
        }

        private async Task ThrowExceptionAsync(ILambdaContext context)
        {
            await Task.CompletedTask;
            throw new System.Exception("my exception");
        }

        private Task<SendMessageResponse> SendSQSMessage(SendMessageRequest sendMessageRequest, CancellationToken cancellationToken = default)
        {
            var response = new SendMessageResponse();
            response.HttpStatusCode = HttpStatusCode.OK;
            return Task.FromResult(response);
        }

        private Task<SendMessageResponse> SendSQSMessageWithBadResult(SendMessageRequest sendMessageRequest, CancellationToken cancellationToken = default)
        {
            var response = new SendMessageResponse();
            response.HttpStatusCode = HttpStatusCode.BadRequest;
            return Task.FromResult(response);
        }

        private Task<SendMessageResponse> SendSQSMessageWithException(SendMessageRequest sendMessageRequest, CancellationToken cancellationToken = default)
        {
            try
            {
                throw new AmazonSQSException(sendMessageRequest.MessageBody);
            }
            catch (AmazonSQSException ex)
            {
                return Task.FromException<SendMessageResponse>(ex);
            }
        }

        private Task<SendMessageBatchResponse> SendSQSBatchMessage(SendMessageBatchRequest sendMessageBatchRequest, CancellationToken cancellationToken = default)
        {
            var response = new SendMessageBatchResponse();
            response.HttpStatusCode = HttpStatusCode.OK;
            return Task.FromResult(response);
        }
        private Task<SendMessageBatchResponse> SendSQSBatchMessageWithBadResult(SendMessageBatchRequest sendMessageBatchRequest, CancellationToken cancellationToken = default)
        {
            var response = new SendMessageBatchResponse();
            response.HttpStatusCode = HttpStatusCode.BadRequest;
            return Task.FromResult(response);
        }
        private Task<SendMessageBatchResponse> SendSQSBatchMessageWithException(SendMessageBatchRequest sendMessageBatchRequest, CancellationToken cancellationToken = default)
        {
            try
            {
                throw new AmazonSQSException(sendMessageBatchRequest.Entries[0].MessageBody);
            }
            catch (AmazonSQSException ex)
            {
                return Task.FromException<SendMessageBatchResponse>(ex);
            }
        }

        private Task<PublishResponse> SendSNSMessage(PublishRequest publishRequest, CancellationToken cancellationToken = default)
        {
            var response = new PublishResponse();
            response.HttpStatusCode = HttpStatusCode.OK;
            return Task.FromResult(response);
        }

        private Task<PublishResponse> SendSNSMessageWithBadResult(PublishRequest publishRequest, CancellationToken cancellationToken = default)
        {
            var response = new PublishResponse();
            response.HttpStatusCode = HttpStatusCode.BadRequest;
            return Task.FromResult(response);
        }

        private Task<PublishResponse> SendSNSMessageWithException(PublishRequest publishRequest, CancellationToken cancellationToken = default)
        {
            try
            {
                throw new AmazonSimpleNotificationServiceException(publishRequest.Message);
            }
            catch (AmazonSimpleNotificationServiceException ex)
            {
                return Task.FromException<PublishResponse>(ex);
            }
        }

        #endregion
    }
}
