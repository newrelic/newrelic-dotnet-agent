// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda
{
    public abstract class LambdaKinesisEventTriggerFixtureBase : LambdaSelfExecutingAssemblyFixture
    {
        private static string GetEventName(bool usesTimeWindow) => $"Kinesis{(usesTimeWindow ? "TimeWindow" : "")}Event";

        protected LambdaKinesisEventTriggerFixtureBase(string targetFramework, bool isAsync, bool usesTimeWindow) :
            base(targetFramework,
                null,
                $"LambdaSelfExecutingAssembly::LambdaSelfExecutingAssembly.Program::{GetEventName(usesTimeWindow)}Handler" + (isAsync ? "Async" : ""),
                GetEventName(usesTimeWindow) + (isAsync ? "Async" : ""),
                null)
        {
        }

        // This event json works for both the normal Kinesis and the KinesisTimeWindowEvent.
        // If we need data specifically defined in the KinesisTimeWindowEvent we can update the tests to separate
        // the two events.
        public void EnqueueEvent()
        {
            var eventJson = """
                {
                    "Records": [
                        {
                            "kinesis": {
                                "kinesisSchemaVersion": "1.0",
                                "partitionKey": "1",
                                "sequenceNumber": "49590338271490256608559692538361571095921575989136588898",
                                "data": "SGVsbG8sIHRoaXMgaXMgYSB0ZXN0Lg==",
                                "approximateArrivalTimestamp": 1545084650.987
                            },
                            "eventSource": "aws:kinesis",
                            "eventVersion": "1.0",
                            "eventID": "shardId-000000000006:49590338271490256608559692538361571095921575989136588898",
                            "eventName": "aws:kinesis:record",
                            "invokeIdentityArn": "arn:aws:iam::111122223333:role/lambda-kinesis-role",
                            "awsRegion": "us-east-2",
                            "eventSourceARN": "arn:aws:kinesis:us-east-2:111122223333:stream/lambda-stream"
                        }
                    ]
                }
                """;
            EnqueueLambdaEvent(eventJson);
        }
    }

    public class LambdaKinesisEventTriggerFixtureNet8 : LambdaKinesisEventTriggerFixtureBase
    {
        public LambdaKinesisEventTriggerFixtureNet8() : base("net8.0", false, false) { }
    }

    public class AsyncLambdaKinesisEventTriggerFixtureNet8 : LambdaKinesisEventTriggerFixtureBase
    {
        public AsyncLambdaKinesisEventTriggerFixtureNet8() : base("net8.0", true, false) { }
    }

    public class LambdaKinesisEventTriggerFixtureNet9 : LambdaKinesisEventTriggerFixtureBase
    {
        public LambdaKinesisEventTriggerFixtureNet9() : base("net9.0", false, false) { }
    }

    public class AsyncLambdaKinesisEventTriggerFixtureNet9 : LambdaKinesisEventTriggerFixtureBase
    {
        public AsyncLambdaKinesisEventTriggerFixtureNet9() : base("net9.0", true, false) { }
    }

    public class LambdaKinesisTimeWindowEventTriggerFixtureNet8 : LambdaKinesisEventTriggerFixtureBase
    {
        public LambdaKinesisTimeWindowEventTriggerFixtureNet8() : base("net8.0", false, true) { }
    }

    public class AsyncLambdaKinesisTimeWindowEventTriggerFixtureNet8 : LambdaKinesisEventTriggerFixtureBase
    {
        public AsyncLambdaKinesisTimeWindowEventTriggerFixtureNet8() : base("net8.0", true, true) { }
    }

    public class LambdaKinesisTimeWindowEventTriggerFixtureNet9 : LambdaKinesisEventTriggerFixtureBase
    {
        public LambdaKinesisTimeWindowEventTriggerFixtureNet9() : base("net9.0", false, true) { }
    }

    public class AsyncLambdaKinesisTimeWindowEventTriggerFixtureNet9 : LambdaKinesisEventTriggerFixtureBase
    {
        public AsyncLambdaKinesisTimeWindowEventTriggerFixtureNet9() : base("net9.0", true, true) { }
    }
}
