// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Attributes;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.WireModels
{
    [TestFixture]
    public class TransactionTraceWireModelTests
    {
        [Test]
        public void TransactionSampleDataSerializesCorrectly()
        {
            // Arrange
            const string expected = @"[1514768400000,1000.0,""Transaction Name"",""Transaction URI"",[1514768400000,{},{},[0.0,1000.0,""Segment Name"",{},[],""Segment Class Name"",""Segment Method Name""],{""agentAttributes"":{},""userAttributes"":{},""intrinsics"":{}}],""Transaction GUID"",null,false,null,null]";
            var timestamp = new DateTime(2018, 1, 1, 1, 0, 0, DateTimeKind.Utc);
            var transactionTraceSegment = new TransactionTraceSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "Segment Name", new Dictionary<string, object>(), new List<TransactionTraceSegment>(), "Segment Class Name", "Segment Method Name");

            var transactionTrace = new TransactionTraceData(timestamp, transactionTraceSegment, new AttributeValueCollection(AttributeDestinations.TransactionTrace));
            var transactionSample = new TransactionTraceWireModel(timestamp, TimeSpan.FromSeconds(1), "Transaction Name", "Transaction URI", transactionTrace, "Transaction GUID", null, null, false);

            // Act
            var actual = JsonConvert.SerializeObject(transactionSample);

            // Assert
            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
