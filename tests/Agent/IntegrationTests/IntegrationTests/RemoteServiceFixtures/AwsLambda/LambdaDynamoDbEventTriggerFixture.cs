// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda
{
    public abstract class LambdaDynamoDbEventTriggerFixtureBase : LambdaSelfExecutingAssemblyFixture
    {
        private static string GetEventName(bool usesTimeWindow) => $"DynamoDb{(usesTimeWindow ? "TimeWindow" : "")}Event";

        protected LambdaDynamoDbEventTriggerFixtureBase(string targetFramework, bool isAsync, bool usesTimeWindow) :
            base(targetFramework,
                null,
                $"LambdaSelfExecutingAssembly::LambdaSelfExecutingAssembly.Program::{GetEventName(usesTimeWindow)}Handler" + (isAsync ? "Async" : ""),
                GetEventName(usesTimeWindow) + (isAsync ? "Async" : ""),
                null)
        {
        }

        // This event json works for both the normal DynamoDBEvent and the DyamoDBTimeWindowEvent.
        // If we need data specifically defined in the DynamoDBTimeWindowEvent we can update the tests to separate
        // the two events.
        public void EnqueueEvent()
        {
            var eventJson = """
                            {
                              "Records": [
                                {
                                  "eventID": "1",
                                  "eventName": "INSERT",
                                  "eventVersion": "1.0",
                                  "eventSource": "aws:dynamodb",
                                  "awsRegion": "{region}",
                                  "dynamodb": {
                                    "Keys": {
                                      "Id": {
                                        "N": "101"
                                      }
                                    },
                                    "NewImage": {
                                      "Message": {
                                        "S": "New item!"
                                      },
                                      "Id": {
                                        "N": "101"
                                      }
                                    },
                                    "SequenceNumber": "111",
                                    "SizeBytes": 26,
                                    "StreamViewType": "NEW_AND_OLD_IMAGES"
                                  },
                                  "eventSourceARN": "arn:{partition}:dynamodb:{region}:account-id:table/ExampleTableWithStream/stream/2015-06-27T00:48:05.899"
                                },
                                {
                                  "eventID": "2",
                                  "eventName": "MODIFY",
                                  "eventVersion": "1.0",
                                  "eventSource": "aws:dynamodb",
                                  "awsRegion": "{region}",
                                  "dynamodb": {
                                    "Keys": {
                                      "Id": {
                                        "N": "101"
                                      }
                                    },
                                    "NewImage": {
                                      "Message": {
                                        "S": "This item has changed"
                                      },
                                      "Id": {
                                        "N": "101"
                                      }
                                    },
                                    "OldImage": {
                                      "Message": {
                                        "S": "New item!"
                                      },
                                      "Id": {
                                        "N": "101"
                                      }
                                    },
                                    "SequenceNumber": "222",
                                    "SizeBytes": 59,
                                    "StreamViewType": "NEW_AND_OLD_IMAGES"
                                  },
                                  "eventSourceARN": "arn:{partition}:dynamodb:{region}:account-id:table/ExampleTableWithStream/stream/2015-06-27T00:48:05.899"
                                },
                                {
                                  "eventID": "3",
                                  "eventName": "REMOVE",
                                  "eventVersion": "1.0",
                                  "eventSource": "aws:dynamodb",
                                  "awsRegion": "{region}",
                                  "dynamodb": {
                                    "Keys": {
                                      "Id": {
                                        "N": "101"
                                      }
                                    },
                                    "OldImage": {
                                      "Message": {
                                        "S": "This item has changed"
                                      },
                                      "Id": {
                                        "N": "101"
                                      }
                                    },
                                    "SequenceNumber": "333",
                                    "SizeBytes": 38,
                                    "StreamViewType": "NEW_AND_OLD_IMAGES"
                                  },
                                  "eventSourceARN": "arn:{partition}:dynamodb:{region}:account-id:table/ExampleTableWithStream/stream/2015-06-27T00:48:05.899"
                                }
                              ]
                            }
                            """;
            EnqueueLambdaEvent(eventJson);
        }
    }

    public class LambdaDynamoDbEventTriggerFixtureNet6 : LambdaDynamoDbEventTriggerFixtureBase
    {
        public LambdaDynamoDbEventTriggerFixtureNet6() : base("net6.0", false, false) { }
    }

    public class AsyncLambdaDynamoDbEventTriggerFixtureNet6 : LambdaDynamoDbEventTriggerFixtureBase
    {
        public AsyncLambdaDynamoDbEventTriggerFixtureNet6() : base("net6.0", true, false) { }
    }

    public class LambdaDynamoDbEventTriggerFixtureNet8 : LambdaDynamoDbEventTriggerFixtureBase
    {
        public LambdaDynamoDbEventTriggerFixtureNet8() : base("net8.0", false, false) { }
    }

    public class AsyncLambdaDynamoDbEventTriggerFixtureNet8 : LambdaDynamoDbEventTriggerFixtureBase
    {
        public AsyncLambdaDynamoDbEventTriggerFixtureNet8() : base("net8.0", true, false) { }
    }

    public class LambdaDynamoDbTimeWindowEventTriggerFixtureNet6 : LambdaDynamoDbEventTriggerFixtureBase
    {
        public LambdaDynamoDbTimeWindowEventTriggerFixtureNet6() : base("net6.0", false, true) { }
    }

    public class AsyncLambdaDynamoDbTimeWindowEventTriggerFixtureNet6 : LambdaDynamoDbEventTriggerFixtureBase
    {
        public AsyncLambdaDynamoDbTimeWindowEventTriggerFixtureNet6() : base("net6.0", true, true) { }
    }

    public class LambdaDynamoDbTimeWindowEventTriggerFixtureNet8 : LambdaDynamoDbEventTriggerFixtureBase
    {
        public LambdaDynamoDbTimeWindowEventTriggerFixtureNet8() : base("net8.0", false, true) { }
    }

    public class AsyncLambdaDynamoDbTimeWindowEventTriggerFixtureNet8 : LambdaDynamoDbEventTriggerFixtureBase
    {
        public AsyncLambdaDynamoDbTimeWindowEventTriggerFixtureNet8() : base("net8.0", true, true) { }
    }
}
