using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace NewRelic.Tests.AwsLambda.AwsLambdaOpenTracerTests
{
    [TestFixture]
    public class DataCollectorTests
    {
        [Test]
        public void DoesWriteDataToCloudWatch()
        {
            var logger = new MockLogger();

            var startTime = DateTimeOffset.UtcNow;
            var span = TestUtil.CreateRootSpan("operationName", startTime, new Dictionary<string, object>(), "testGuid", logger: logger);

            span.RootSpan.PrioritySamplingState.Sampled = true;

            span.Finish();

            var deserializedPayload = JsonConvert.DeserializeObject<object[]>(logger.LastLogMessage);
            var data = TestUtil.DecodeAndDecompressNewRelicPayload(deserializedPayload[3] as string);

            Assert.IsTrue(logger.LastLogMessage.Contains("NR_LAMBDA_MONITORING"));
            Assert.IsTrue(data.Contains("analytic_event_data"));
            Assert.IsTrue(data.Contains("span_event_data"));
        }
    }
}
