// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda
{
    public abstract class LambdaSnsEventTriggerFixtureBase : LambdaSelfExecutingAssemblyFixture
    {
        private static string GetHandlerString(bool isAsync)
        {
            return "LambdaSelfExecutingAssembly::LambdaSelfExecutingAssembly.Program::SnsHandler" + (isAsync ? "Async" : "");
        }

        protected LambdaSnsEventTriggerFixtureBase(string targetFramework, bool isAsync, bool useNewRelicHandler) :
            base(targetFramework,
                useNewRelicHandler ? GetHandlerString(isAsync) : null,
                useNewRelicHandler ? null : GetHandlerString(isAsync),
                "SnsHandler" + (isAsync ? "Async" : ""),
                "1.0")
        {
        }

        public void EnqueueSnsEvent()
        {
            var snsJson = """
                          {
                            "Records": [
                              {
                                "EventSource": "aws:sns",
                                "EventVersion": "1.0",
                                "EventSubscriptionArn": "arn:{partition}:sns:EXAMPLE1",
                                "Sns": {
                                  "Type": "Notification",
                                  "MessageId": "95df01b4-ee98-5cb9-9903-4c221d41eb5e",
                                  "TopicArn": "arn:{partition}:sns:EXAMPLE2",
                                  "Subject": "TestInvoke",
                                  "Message": "Hello from SNS!",
                                  "Timestamp": "1970-01-01T00:00:00Z",
                                  "SignatureVersion": "1",
                                  "Signature": "EXAMPLE",
                                  "SigningCertUrl": "EXAMPLE",
                                  "UnsubscribeUrl": "EXAMPLE",
                                  "MessageAttributes": {
                                    "Test": {
                                      "Type": "String",
                                      "Value": "TestString"
                                    },
                                    "TestBinary": {
                                      "Type": "Binary",
                                      "Value": "TestBinary"
                                    }
                                  }
                                }
                              }
                            ]
                          }
                          """;
            EnqueueLambdaEvent(snsJson);
        }

        public void EnqueueSnsEventWithDTHeaders(string traceId, string spanId)
        {
            var snsJson = $$"""
                            {
                              "Records": [
                                {
                                  "EventSource": "aws:sns",
                                  "EventVersion": "1.0",
                                  "EventSubscriptionArn": "arn:{partition}:sns:EXAMPLE1",
                                  "Sns": {
                                    "Type": "Notification",
                                    "MessageId": "95df01b4-ee98-5cb9-9903-4c221d41eb5e",
                                    "TopicArn": "arn:{partition}:sns:EXAMPLE2",
                                    "Subject": "TestInvoke",
                                    "Message": "Hello from SNS!",
                                    "Timestamp": "1970-01-01T00:00:00Z",
                                    "SignatureVersion": "1",
                                    "Signature": "EXAMPLE",
                                    "SigningCertUrl": "EXAMPLE",
                                    "UnsubscribeUrl": "EXAMPLE",
                                    "MessageAttributes": {
                                      "Test": {
                                        "Type": "String",
                                        "Value": "TestString"
                                      },
                                      "TestBinary": {
                                        "Type": "Binary",
                                        "Value": "TestBinary"
                                      },
                                      "traceparent": {
                                        "Type": "String",
                                        "Value": "{{GetTestTraceParentHeaderValue(traceId, spanId)}}"
                                      },
                                      "tracestate": {
                                        "Type": "String",
                                        "Value": "{{GetTestTraceStateHeaderValue(spanId)}}"
                                      }
                                    }
                                  }
                                }
                              ]
                            }
                            """;
            EnqueueLambdaEvent(snsJson);
        }
    }

    public class LambdaSnsEventTriggerFixtureNet6 : LambdaSnsEventTriggerFixtureBase
    {
        public LambdaSnsEventTriggerFixtureNet6() : base("net6.0", false, true) { }
    }

    public class AsyncLambdaSnsEventTriggerFixtureNet6 : LambdaSnsEventTriggerFixtureBase
    {
        public AsyncLambdaSnsEventTriggerFixtureNet6() : base("net6.0", true, true) { }
    }

    public class LambdaHandlerOnlySnsTriggerFixtureNet6 : LambdaSnsEventTriggerFixtureBase
    {
        public LambdaHandlerOnlySnsTriggerFixtureNet6() : base("net6.0", false, false) { }
    }

    public class AsyncLambdaHandlerOnlySnsTriggerFixtureNet6 : LambdaSnsEventTriggerFixtureBase
    {
        public AsyncLambdaHandlerOnlySnsTriggerFixtureNet6() : base("net6.0", true, false) { }
    }

    public class LambdaSnsEventTriggerFixtureNet8 : LambdaSnsEventTriggerFixtureBase
    {
        public LambdaSnsEventTriggerFixtureNet8() : base("net8.0", false, true) { }
    }

    public class AsyncLambdaSnsEventTriggerFixtureNet8 : LambdaSnsEventTriggerFixtureBase
    {
        public AsyncLambdaSnsEventTriggerFixtureNet8() : base("net8.0", true, true) { }
    }

    public class LambdaHandlerOnlySnsTriggerFixtureNet8 : LambdaSnsEventTriggerFixtureBase
    {
        public LambdaHandlerOnlySnsTriggerFixtureNet8() : base("net8.0", false, false) { }
    }

    public class AsyncLambdaHandlerOnlySnsTriggerFixtureNet8 : LambdaSnsEventTriggerFixtureBase
    {
        public AsyncLambdaHandlerOnlySnsTriggerFixtureNet8() : base("net8.0", true, false) { }
    }
}
