// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
using System;

namespace NewRelic.Agent.Core.Segments.Tests
{
    [TestFixture]
    public class SegmentTests
    {
        [Test]
        public void End_WithException_HasErrorData()
        {
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("Type", "Method", 1));

            segment.End(new Exception("Unhandled exception"));

            Assert.IsNotNull(segment.ErrorData);
            Assert.AreEqual("System.Exception", segment.ErrorData.ErrorTypeName);
            Assert.AreEqual("Unhandled exception", segment.ErrorData.ErrorMessage);
        }
    }
}
