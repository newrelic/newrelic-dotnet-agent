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

    public class LambdaSnsEventTriggerFixtureCoreOldest : LambdaSnsEventTriggerFixtureBase
    {
        public LambdaSnsEventTriggerFixtureCoreOldest() : base(CoreOldestTFM, false, true) { }
    }

    public class AsyncLambdaSnsEventTriggerFixtureCoreOldest : LambdaSnsEventTriggerFixtureBase
    {
        public AsyncLambdaSnsEventTriggerFixtureCoreOldest() : base(CoreOldestTFM, true, true) { }
    }

    public class LambdaHandlerOnlySnsTriggerFixtureCoreOldest : LambdaSnsEventTriggerFixtureBase
    {
        public LambdaHandlerOnlySnsTriggerFixtureCoreOldest() : base(CoreOldestTFM, false, false) { }
    }

    public class AsyncLambdaHandlerOnlySnsTriggerFixtureCoreOldest : LambdaSnsEventTriggerFixtureBase
    {
        public AsyncLambdaHandlerOnlySnsTriggerFixtureCoreOldest() : base(CoreOldestTFM, true, false) { }
    }

    public class LambdaSnsEventTriggerFixtureCoreLatest : LambdaSnsEventTriggerFixtureBase
    {
        public LambdaSnsEventTriggerFixtureCoreLatest() : base(CoreLatestTFM, false, true) { }
    }

    public class AsyncLambdaSnsEventTriggerFixtureCoreLatest : LambdaSnsEventTriggerFixtureBase
    {
        public AsyncLambdaSnsEventTriggerFixtureCoreLatest() : base(CoreLatestTFM, true, true) { }
    }

    public class LambdaHandlerOnlySnsTriggerFixtureCoreLatest : LambdaSnsEventTriggerFixtureBase
    {
        public LambdaHandlerOnlySnsTriggerFixtureCoreLatest() : base(CoreLatestTFM, false, false) { }
    }

    public class AsyncLambdaHandlerOnlySnsTriggerFixtureCoreLatest : LambdaSnsEventTriggerFixtureBase
    {
        public AsyncLambdaHandlerOnlySnsTriggerFixtureCoreLatest() : base(CoreLatestTFM, true, false) { }
    }
}
