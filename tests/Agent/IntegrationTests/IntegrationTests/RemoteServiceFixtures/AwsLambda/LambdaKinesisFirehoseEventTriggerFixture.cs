// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda
{
    public abstract class LambdaKinesisFirehoseEventTriggerFixtureBase : LambdaSelfExecutingAssemblyFixture
    {
        protected LambdaKinesisFirehoseEventTriggerFixtureBase(string targetFramework, bool isAsync) :
            base(targetFramework,
                null,
                "LambdaSelfExecutingAssembly::LambdaSelfExecutingAssembly.Program::KinesisFirehoseEventHandler" + (isAsync ? "Async" : ""),
                "KinesisFirehoseEvent" + (isAsync ? "Async" : ""),
                null)
        {
        }

        public void EnqueueEvent()
        {
            var eventJson = """
                {
                  "invocationId": "invoked123",
                  "deliveryStreamArn": "aws:lambda:events",
                  "region": "us-west-2",
                  "records": [
                    {
                      "data": "SGVsbG8gV29ybGQ=",
                      "recordId": "record1",
                      "approximateArrivalTimestamp": 1510772160000,
                      "kinesisRecordMetadata": {
                        "shardId": "shardId-000000000000",
                        "partitionKey": "4d1ad2b9-24f8-4b9d-a088-76e9947c317a",
                        "approximateArrivalTimestamp": "2012-04-23T18:25:43.511Z",
                        "sequenceNumber": "49546986683135544286507457936321625675700192471156785154",
                        "subsequenceNumber": ""
                      }
                    },
                    {
                      "data": "SGVsbG8gV29ybGQ=",
                      "recordId": "record2",
                      "approximateArrivalTimestamp": 151077216000,
                      "kinesisRecordMetadata": {
                        "shardId": "shardId-000000000001",
                        "partitionKey": "4d1ad2b9-24f8-4b9d-a088-76e9947c318a",
                        "approximateArrivalTimestamp": "2012-04-23T19:25:43.511Z",
                        "sequenceNumber": "49546986683135544286507457936321625675700192471156785155",
                        "subsequenceNumber": ""
                      }
                    }
                  ]
                }
                """;
            EnqueueLambdaEvent(eventJson);
        }
    }

    public class LambdaKinesisFirehoseEventTriggerFixtureNet6 : LambdaKinesisFirehoseEventTriggerFixtureBase
    {
        public LambdaKinesisFirehoseEventTriggerFixtureNet6() : base("net6.0", false) { }
    }

    public class AsyncLambdaKinesisFirehoseEventTriggerFixtureNet6 : LambdaKinesisFirehoseEventTriggerFixtureBase
    {
        public AsyncLambdaKinesisFirehoseEventTriggerFixtureNet6() : base("net6.0", true) { }
    }

    public class LambdaKinesisFirehoseEventTriggerFixtureNet8 : LambdaKinesisFirehoseEventTriggerFixtureBase
    {
        public LambdaKinesisFirehoseEventTriggerFixtureNet8() : base("net8.0", false) { }
    }

    public class AsyncLambdaKinesisFirehoseEventTriggerFixtureNet8 : LambdaKinesisFirehoseEventTriggerFixtureBase
    {
        public AsyncLambdaKinesisFirehoseEventTriggerFixtureNet8() : base("net8.0", true) { }
    }
}
