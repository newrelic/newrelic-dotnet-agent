// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.TestUtilities;
using NewRelic.SystemInterfaces;
using Newtonsoft.Json;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DataTransport
{
    [TestFixture]
    public class ServerlessModePayloadManagerTests
    {
        private ServerlessModePayloadManager _serverlessPayloadManager;
        private IEnvironment _environment;
        private IFileWrapper _fileWrapper;

        [SetUp]
        public void Setup()
        {
            _fileWrapper = Mock.Create<IFileWrapper>();
            _environment = Mock.Create<IEnvironment>();

            _serverlessPayloadManager = new ServerlessModePayloadManager(_fileWrapper, _environment);

        }
        [TearDown]
        public void TearDown()
        {
        }

        [Test]
        public void BuildPayload_Metadata_IsCorrect()
        {
            // Arrange
            var testExecutionEnv = "TEST_EXECUTION_ENV";
            var testFunctionVersion = "TestFunctionVersion";
            var testArn = "TestArn";

            Mock.Arrange(() => _environment.GetEnvironmentVariable("AWS_EXECUTION_ENV")).Returns(testExecutionEnv);

            // Act
            ServerlessModePayloadManager.SetMetadata(testFunctionVersion, testArn);
            var payload = _serverlessPayloadManager.BuildPayload(new WireData());

            // Assert
            var payloadObj = JsonConvert.DeserializeObject<List<object>>(payload);

            Assert.That(payloadObj, Is.Not.Null);
            Assert.That(payloadObj, Has.Count.EqualTo(4));
            Assert.That(payloadObj[0].ToString(), Is.EqualTo("2"));
            Assert.That(payloadObj[1].ToString(), Is.EqualTo("NR_LAMBDA_MONITORING"));

            dynamic metaData = payloadObj[2];
            Assert.That(metaData["protocol_version"].Value, Is.EqualTo(17));
            Assert.That(metaData["agent_version"].Value, Is.Not.Null);
            Assert.That(metaData["metadata_version"].Value, Is.EqualTo(2));
            Assert.That(metaData["agent_language"].Value, Is.EqualTo("dotnet"));
            Assert.That(metaData["execution_environment"].Value, Is.EqualTo(testExecutionEnv));
            Assert.That(metaData["function_version"].Value, Is.EqualTo(testFunctionVersion));
            Assert.That(metaData["arn"].Value, Is.EqualTo(testArn));
        }

        [Test]
        public void BuildPayload_CompressedPayload_DecompressesCorrectly()
        {
            // Arrange
            var testExecutionEnv = "TEST_EXECUTION_ENV";
            var testFunctionVersion = "TestFunctionVersion";
            var testArn = "TestArn";

            Mock.Arrange(() => _environment.GetEnvironmentVariable("AWS_EXECUTION_ENV")).Returns(testExecutionEnv);

            const string expected = @"[1514768400000,1000.0,""Transaction Name"",""Transaction URI"",[1514768400000,{},{},[0.0,1000.0,""Segment Name"",{},[],""Segment Class Name"",""Segment Method Name""],{""agentAttributes"":{},""userAttributes"":{},""intrinsics"":{}}],""Transaction GUID"",null,false,null,null]";
            var timestamp = new DateTime(2018, 1, 1, 1, 0, 0, DateTimeKind.Utc);
            var transactionTraceSegment = new TransactionTraceSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "Segment Name", new Dictionary<string, object>(), new List<TransactionTraceSegment>(), "Segment Class Name", "Segment Method Name");

            var transactionTrace = new TransactionTraceData(timestamp, transactionTraceSegment, new AttributeValueCollection(AttributeDestinations.TransactionTrace));
            var transactionSample = new TransactionTraceWireModel(timestamp, TimeSpan.FromSeconds(1), "Transaction Name", "Transaction URI", transactionTrace, "Transaction GUID", null, null, false);

            var wireData = new WireData();
            wireData["transaction_sample_data"] = new [] {transactionSample };

            // Act
            ServerlessModePayloadManager.SetMetadata(testFunctionVersion, testArn);
            var payload = _serverlessPayloadManager.BuildPayload(wireData);

            // Assert
            var unzippedPayload = payload.GetUnzippedPayload();

            Assert.That(unzippedPayload, Is.EqualTo($"{{\"transaction_sample_data\":[{expected}]}}"));
        }

        [Test]
        public void WritePayload_WritesCorrectly()
        {
                        // Arrange
            var testExecutionEnv = "TEST_EXECUTION_ENV";
            var testFunctionVersion = "TestFunctionVersion";
            var testArn = "TestArn";

            // intercept the file output and write it to a memory stream instead
            var actualMS = new MemoryStream();
            FileStream fs = Mock.Create<FileStream>(Constructor.Mocked);
            Mock.Arrange(() => fs.Write(null, 0, 0)).IgnoreArguments()
                .DoInstead((byte[] content, int offset, int len) => actualMS.Write(content, 0, content.Length));
            Mock.Arrange(() => _fileWrapper.Exists(Arg.IsAny<string>())).Returns(true);
            Mock.Arrange(() => _fileWrapper.OpenWrite(Arg.IsAny<string>())).Returns(fs);

            Mock.Arrange(() => _environment.GetEnvironmentVariable("AWS_EXECUTION_ENV")).Returns(testExecutionEnv);

            //const string expected = @"[1514768400000,1000.0,""Transaction Name"",""Transaction URI"",[1514768400000,{},{},[0.0,1000.0,""Segment Name"",{},[],""Segment Class Name"",""Segment Method Name""],{""agentAttributes"":{},""userAttributes"":{},""intrinsics"":{}}],""Transaction GUID"",null,false,null,null]";
            var timestamp = new DateTime(2018, 1, 1, 1, 0, 0, DateTimeKind.Utc);
            var transactionTraceSegment = new TransactionTraceSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "Segment Name", new Dictionary<string, object>(), new List<TransactionTraceSegment>(), "Segment Class Name", "Segment Method Name");

            var transactionTrace = new TransactionTraceData(timestamp, transactionTraceSegment, new AttributeValueCollection(AttributeDestinations.TransactionTrace));
            var transactionSample = new TransactionTraceWireModel(timestamp, TimeSpan.FromSeconds(1), "Transaction Name", "Transaction URI", transactionTrace, "Transaction GUID", null, null, false);

            var wireData = new WireData();
            wireData["transaction_sample_data"] = new [] {transactionSample };

            // Act
            ServerlessModePayloadManager.SetMetadata(testFunctionVersion, testArn);
            var payload = _serverlessPayloadManager.BuildPayload(wireData);
            _serverlessPayloadManager.WritePayload(payload, "gibberish");

            // Assert
            actualMS.Position = 0;
            var actualBytes = actualMS.ToArray();
            // the payload is encoded as UTF8 bytes when writing to the output file.
            Assert.That(Encoding.UTF8.GetBytes(payload), Is.EqualTo(actualBytes));
        }
    }
}
