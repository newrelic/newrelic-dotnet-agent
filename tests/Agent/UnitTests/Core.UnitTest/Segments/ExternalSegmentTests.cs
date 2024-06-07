// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.Agent.Core.Spans;

namespace NewRelic.Agent.Core.Segments.Tests
{

    [TestFixture]
    public class ExternalSegmentTests
    {
        private const string TransactionGuidSegmentParameterKey = "transaction_guid";



        [Test]
        public void Build_IncludesCatParameter_IfCatResponseDataIsSet()
        {
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("foo", "bar", 1));
            segment.SetSegmentData(new ExternalSegmentData(new Uri("http://www.google.com"), "method", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3, "guid", false)));
            segment.End();

            Assert.Multiple(() =>
            {
                Assert.That(segment.Parameters.ToDictionary().ContainsKey(TransactionGuidSegmentParameterKey), Is.True);
                Assert.That(segment.Parameters.ToDictionary()[TransactionGuidSegmentParameterKey], Is.EqualTo("guid"));
            });
        }

        [Test]
        public void Build_DoesNotIncludeCatParameter_IfCatResponseDataIsNotSet()
        {
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("foo", "bar", 1));
            segment.SetSegmentData(new ExternalSegmentData(new Uri("http://www.google.com"), "method"));

            Assert.That(segment.Parameters.ToDictionary().ContainsKey(TransactionGuidSegmentParameterKey), Is.False);
        }
    }
}
