// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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

        [Test]
        public void DoesWriteDataToNamedPipe()
        {
            var fileSystemManager = new MockFileSystemManager();
            fileSystemManager.PathExists = true;

            var startTime = DateTimeOffset.UtcNow;
            var span = TestUtil.CreateRootSpan("operationName", startTime, new Dictionary<string, object>(), "testGuid", fileSystemManager: fileSystemManager);

            span.RootSpan.PrioritySamplingState.Sampled = true;

            span.Finish();

            var deserializedPayload = JsonConvert.DeserializeObject<object[]>(fileSystemManager.FileContents);
            var data = TestUtil.DecodeAndDecompressNewRelicPayload(deserializedPayload[3] as string);

            Assert.IsTrue(fileSystemManager.FileContents.Contains("NR_LAMBDA_MONITORING"));
            Assert.IsTrue(data.Contains("analytic_event_data"));
            Assert.IsTrue(data.Contains("span_event_data"));
        }
    }
}
