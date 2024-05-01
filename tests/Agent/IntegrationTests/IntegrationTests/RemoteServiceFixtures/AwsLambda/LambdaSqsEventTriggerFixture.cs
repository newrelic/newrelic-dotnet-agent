// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda
{
    public abstract class LambdaSqsEventTriggerFixtureBase : LambdaSelfExecutingAssemblyFixture
    {
        private static string GetHandlerString(bool isAsync)
        {
            return "LambdaSelfExecutingAssembly::LambdaSelfExecutingAssembly.Program::SqsHandler" + (isAsync ? "Async" : "");
        }

        protected LambdaSqsEventTriggerFixtureBase(string targetFramework, bool isAsync, bool useNewRelicHandler) :
            base(targetFramework,
                useNewRelicHandler ? GetHandlerString(isAsync) : null,
                useNewRelicHandler ? null : GetHandlerString(isAsync),
                "SqsHandler" + (isAsync ? "Async" : ""),
                "1.0")
        {
        }

        public void EnqueueSqsEvent()
        {
            var sqsJson = """
                          {
                            "Records": [
                              {
                                "messageId": "19dd0b57-b21e-4ac1-bd88-01bbb068cb78",
                                "receiptHandle": "MessageReceiptHandle",
                                "body": "Hello from SQS!",
                                "attributes": {
                                  "ApproximateReceiveCount": "1",
                                  "SentTimestamp": "1523232000000",
                                  "SenderId": "123456789012",
                                  "ApproximateFirstReceiveTimestamp": "1523232000001"
                                },
                                "messageAttributes": {
                                  "Test": {
                                    "Type": "String",
                                    "StringValue": "TestString"
                                  },
                                  "TestBinary": {
                                    "Type": "Binary",
                                    "BinaryValue": "VGVzdEJpbmFyeQ=="
                                  }
                                },
                                "md5OfBody": "7b270e59b47ff90a553787216d55d91d",
                                "eventSource": "aws:sqs",
                                "eventSourceARN": "arn:{partition}:sqs:{region}:123456789012:MyQueue",
                                "awsRegion": "us-west-2"
                              }
                            ]
                          }
                          """;
            EnqueueLambdaEvent(sqsJson);
        }

        public void EnqueueSqsEventWithDTHeaders(string traceId, string spanId)
        {
            var sqsJson = $$"""
                          {
                            "Records": [
                              {
                                "messageId": "19dd0b57-b21e-4ac1-bd88-01bbb068cb78",
                                "receiptHandle": "MessageReceiptHandle",
                                "body": "Hello from SQS!",
                                "attributes": {
                                  "ApproximateReceiveCount": "1",
                                  "SentTimestamp": "1523232000000",
                                  "SenderId": "123456789012",
                                  "ApproximateFirstReceiveTimestamp": "1523232000001"
                                },
                                "messageAttributes": {
                                  "Test": {
                                    "Type": "String",
                                    "StringValue": "TestString"
                                  },
                                  "TestBinary": {
                                    "Type": "Binary",
                                    "BinaryValue": "VGVzdEJpbmFyeQ=="
                                  },
                                   "traceparent": {
                                    "Type": "String",
                                    "StringValue": "{{GetTestTraceParentHeaderValue(traceId, spanId)}}"
                                  },
                                  "tracestate": {
                                    "Type": "String",
                                    "StringValue": "{{GetTestTraceStateHeaderValue(spanId)}}"
                                  }
                                },
                                "md5OfBody": "7b270e59b47ff90a553787216d55d91d",
                                "eventSource": "aws:sqs",
                                "eventSourceARN": "arn:{partition}:sqs:{region}:123456789012:MyQueue",
                                "awsRegion": "us-west-2"
                              }
                            ]
                          }
                          """;
            EnqueueLambdaEvent(sqsJson);
        }
    }

    public class LambdaSqsEventTriggerFixtureNet6 : LambdaSqsEventTriggerFixtureBase
    {
        public LambdaSqsEventTriggerFixtureNet6() : base("net6.0", false, true) { }
    }

    public class AsyncLambdaSqsEventTriggerFixtureNet6 : LambdaSqsEventTriggerFixtureBase
    {
        public AsyncLambdaSqsEventTriggerFixtureNet6() : base("net6.0", true, true) { }
    }

    public class LambdaSqsEventTriggerFixtureNet8 : LambdaSqsEventTriggerFixtureBase
    {
        public LambdaSqsEventTriggerFixtureNet8() : base("net8.0", false, true) { }
    }

    public class AsyncLambdaSqsEventTriggerFixtureNet8 : LambdaSqsEventTriggerFixtureBase
    {
        public AsyncLambdaSqsEventTriggerFixtureNet8() : base("net8.0", true, true) { }
    }
}
