// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Extensions.SystemExtensions.Collections.Generic;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Segments.Tests;

[TestFixture]
public class ExternalSegmentTests
{
    private const string TransactionGuidSegmentParameterKey = "transaction_guid";

    [Test]
    public void Build_IncludesCatParameter_IfCatResponseDataIsSet()
    {
        var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("foo", "bar", 1));
        segment.SetSegmentData(new ExternalSegmentData(new Uri("http://www.google.com"), "method", crossApplicationResponseData: new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3, "guid", false)));
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

    [TestCase("overrode")]
    [TestCase(null)]
    public void ExternalGrpcSegmentData_Success(string componentOverride)
    {
        var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("foo", "bar", 1));
        segment.SetSegmentData(new ExternalGrpcSegmentData(new Uri("http://www.google.com"), "method", componentOverride: componentOverride));

        var grpcSegmentData = segment.SegmentData as ExternalGrpcSegmentData;
        Assert.That(grpcSegmentData, Is.Not.Null);
        Assert.That(grpcSegmentData.Uri, Is.EqualTo(new Uri("http://www.google.com")));
        Assert.That(grpcSegmentData.Method, Is.EqualTo("method"));
    }
}
