// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures.AwsLambda
{
    public abstract class LambdaScheduledCloudWatchEventTriggerFixtureBase : LambdaSelfExecutingAssemblyFixture
    {
        protected LambdaScheduledCloudWatchEventTriggerFixtureBase(string targetFramework, bool isAsync) :
            base(targetFramework,
                null,
                "LambdaSelfExecutingAssembly::LambdaSelfExecutingAssembly.Program::ScheduledCloudWatchEventHandler" + (isAsync ? "Async" : ""),
                "ScheduledCloudWatchEvent" + (isAsync ? "Async" : ""),
                null)
        {
        }

        public void EnqueueEvent()
        {
            var eventJson = @"{
  ""version"": ""0"",
  ""account"": ""123456789012"",
  ""region"": ""us-east-2"",
  ""detail"": {},
  ""detail-type"": ""Scheduled Event"",
  ""source"": ""aws.events"",
  ""time"": ""2019-03-01T01:23:45Z"",
  ""id"": ""cdc73f9d-aea9-11e3-9d5a-835b769c0d9c"",
  ""resources"": [
    ""arn:aws:events:us-east-2:123456789012:rule/my-schedule""
  ]
}";
            EnqueueLambdaEvent(eventJson);
        }
    }

    public class LambdaScheduledCloudWatchEventTriggerFixtureNet6 : LambdaScheduledCloudWatchEventTriggerFixtureBase
    {
        public LambdaScheduledCloudWatchEventTriggerFixtureNet6() : base("net6.0", false) { }
    }

    public class AsyncLambdaScheduledCloudWatchEventTriggerFixtureNet6 : LambdaScheduledCloudWatchEventTriggerFixtureBase
    {
        public AsyncLambdaScheduledCloudWatchEventTriggerFixtureNet6() : base("net6.0", true) { }
    }

    public class LambdaScheduledCloudWatchEventTriggerFixtureNet8 : LambdaScheduledCloudWatchEventTriggerFixtureBase
    {
        public LambdaScheduledCloudWatchEventTriggerFixtureNet8() : base("net8.0", false) { }
    }

    public class AsyncLambdaScheduledCloudWatchEventTriggerFixtureNet8 : LambdaScheduledCloudWatchEventTriggerFixtureBase
    {
        public AsyncLambdaScheduledCloudWatchEventTriggerFixtureNet8() : base("net8.0", true) { }
    }
}
