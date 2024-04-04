// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public abstract class LambdaSelfExecutingAssemblyFixture : LambdaTestToolFixture
    {
        public LambdaSelfExecutingAssemblyFixture(string newRelicLambdaHandler, string lambdaHandler, string lambdaName, string lambdaVersion, string lambdaExecutionEnvironment) :
            base(new RemoteService("LambdaSelfExecutingAssembly", "LambdaSelfExecutingAssembly.exe", "net6.0", ApplicationType.Bounded, createsPidFile: true, isCoreApp: true, publishApp: true),
                newRelicLambdaHandler,
                lambdaHandler,
                lambdaName,
                lambdaVersion,
                lambdaExecutionEnvironment)
        {
        }
    }

    public class LambdaSnsEventTriggerFixture : LambdaSelfExecutingAssemblyFixture
    {
        public LambdaSnsEventTriggerFixture() :
            base("LambdaSelfExecutingAssembly::LambdaSelfExecutingAssembly.Program::SnsHandler",
                null,
                "SnsHandler",
                "1.0",
                "self executing assembly")
        {
        }

        public void EnqueueSnsEvent()
        {
            var snsJson = @"{
  ""Records"": [
    {
      ""EventSource"": ""aws:sns"",
      ""EventVersion"": ""1.0"",
      ""EventSubscriptionArn"": ""arn:{partition}:sns:EXAMPLE"",
      ""Sns"": {
        ""Type"": ""Notification"",
        ""MessageId"": ""95df01b4-ee98-5cb9-9903-4c221d41eb5e"",
        ""TopicArn"": ""arn:{partition}:sns:EXAMPLE"",
        ""Subject"": ""TestInvoke"",
        ""Message"": ""Hello from SNS!"",
        ""Timestamp"": ""1970-01-01T00:00:00Z"",
        ""SignatureVersion"": ""1"",
        ""Signature"": ""EXAMPLE"",
        ""SigningCertUrl"": ""EXAMPLE"",
        ""UnsubscribeUrl"": ""EXAMPLE"",
        ""MessageAttributes"": {
          ""Test"": {
            ""Type"": ""String"",
            ""Value"": ""TestString""
          },
          ""TestBinary"": {
            ""Type"": ""Binary"",
            ""Value"": ""TestBinary""
          }
        }
      }
    }
  ]
}";
            EnqueueLambdaEvent(snsJson);
        }
    }
}
