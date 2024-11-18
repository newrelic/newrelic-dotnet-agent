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

    public class LambdaScheduledCloudWatchEventTriggerFixtureCoreOldest : LambdaScheduledCloudWatchEventTriggerFixtureBase
    {
        public LambdaScheduledCloudWatchEventTriggerFixtureCoreOldest() : base(CoreOldestTFM, false) { }
    }

    public class AsyncLambdaScheduledCloudWatchEventTriggerFixtureCoreOldest : LambdaScheduledCloudWatchEventTriggerFixtureBase
    {
        public AsyncLambdaScheduledCloudWatchEventTriggerFixtureCoreOldest() : base(CoreOldestTFM, true) { }
    }

    public class LambdaScheduledCloudWatchEventTriggerFixtureCoreLatest : LambdaScheduledCloudWatchEventTriggerFixtureBase
    {
        public LambdaScheduledCloudWatchEventTriggerFixtureCoreLatest() : base(CoreLatestTFM, false) { }
    }

    public class AsyncLambdaScheduledCloudWatchEventTriggerFixtureCoreLatest : LambdaScheduledCloudWatchEventTriggerFixtureBase
    {
        public AsyncLambdaScheduledCloudWatchEventTriggerFixtureCoreLatest() : base(CoreLatestTFM, true) { }
    }
}
