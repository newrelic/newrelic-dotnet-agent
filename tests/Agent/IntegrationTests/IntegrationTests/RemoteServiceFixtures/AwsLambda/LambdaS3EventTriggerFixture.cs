// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda
{
    public abstract class LambdaS3EventTriggerFixtureBase : LambdaSelfExecutingAssemblyFixture
    {
        protected LambdaS3EventTriggerFixtureBase(string targetFramework, bool isAsync) :
            base(targetFramework,
                null,
                "LambdaSelfExecutingAssembly::LambdaSelfExecutingAssembly.Program::S3EventHandler" + (isAsync ? "Async" : ""),
                "S3Event" + (isAsync ? "Async" : ""),
                null)
        {
        }

        public void EnqueueS3PutEvent()
        {
            var eventJson = """
                            {
                              "Records": [
                                {
                                  "eventVersion": "2.0",
                                  "eventSource": "aws:s3",
                                  "awsRegion": "{region}",
                                  "eventTime": "1970-01-01T00:00:00Z",
                                  "eventName": "ObjectCreated:Put",
                                  "userIdentity": {
                                    "principalId": "EXAMPLE"
                                  },
                                  "requestParameters": {
                                    "sourceIPAddress": "127.0.0.1"
                                  },
                                  "responseElements": {
                                    "x-amz-request-id": "EXAMPLE123456789",
                                    "x-amz-id-2": "EXAMPLE123/5678abcdefghijklambdaisawesome/mnopqrstuvwxyzABCDEFGH"
                                  },
                                  "s3": {
                                    "s3SchemaVersion": "1.0",
                                    "configurationId": "testConfigRule",
                                    "bucket": {
                                      "name": "sourcebucket",
                                      "ownerIdentity": {
                                        "principalId": "EXAMPLE"
                                      },
                                      "arn": "arn:{partition}:s3:::mybucket"
                                    },
                                    "object": {
                                      "key": "HappyFace.jpg",
                                      "size": 1024,
                                      "eTag": "0123456789abcdef0123456789abcdef",
                                      "sequencer": "0A1B2C3D4E5F678901"
                                    }
                                  }
                                }
                              ]
                            }
                            """;
            EnqueueLambdaEvent(eventJson);
        }

        public void EnqueueS3DeleteEvent()
        {
            var eventJson = """
                            {
                              "Records": [
                                {
                                  "eventVersion": "2.0",
                                  "eventSource": "aws:s3",
                                  "awsRegion": "{region}",
                                  "eventTime": "1970-01-01T00:00:00Z",
                                  "eventName": "ObjectRemoved:Delete",
                                  "userIdentity": {
                                    "principalId": "EXAMPLE"
                                  },
                                  "requestParameters": {
                                    "sourceIPAddress": "127.0.0.1"
                                  },
                                  "responseElements": {
                                    "x-amz-request-id": "EXAMPLE123456789",
                                    "x-amz-id-2": "EXAMPLE123/5678abcdefghijklambdaisawesome/mnopqrstuvwxyzABCDEFGH"
                                  },
                                  "s3": {
                                    "s3SchemaVersion": "1.0",
                                    "configurationId": "testConfigRule",
                                    "bucket": {
                                      "name": "sourcebucket",
                                      "ownerIdentity": {
                                        "principalId": "EXAMPLE"
                                      },
                                      "arn": "arn:{partition}:s3:::mybucket"
                                    },
                                    "object": {
                                      "key": "HappyFace.jpg",
                                      "sequencer": "0A1B2C3D4E5F678901"
                                    }
                                  }
                                }
                              ]
                            }
                            """;
            EnqueueLambdaEvent(eventJson);
        }
    }

    public class LambdaS3EventTriggerFixtureNet6 : LambdaS3EventTriggerFixtureBase
    {
        public LambdaS3EventTriggerFixtureNet6() : base("net6.0", false) { }
    }

    public class AsyncLambdaS3EventTriggerFixtureNet6 : LambdaS3EventTriggerFixtureBase
    {
        public AsyncLambdaS3EventTriggerFixtureNet6() : base("net6.0", true) { }
    }

    public class LambdaS3EventTriggerFixtureNet8 : LambdaS3EventTriggerFixtureBase
    {
        public LambdaS3EventTriggerFixtureNet8() : base("net8.0", false) { }
    }

    public class AsyncLambdaS3EventTriggerFixtureNet8 : LambdaS3EventTriggerFixtureBase
    {
        public AsyncLambdaS3EventTriggerFixtureNet8() : base("net8.0", true) { }
    }
}
