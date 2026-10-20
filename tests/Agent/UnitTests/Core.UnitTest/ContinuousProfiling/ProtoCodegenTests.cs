// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using OpenTelemetry.Proto.Profiles.V1Development;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

[TestFixture]
public class ProtoCodegenTests
{
    [Test]
    public void Profile_type_is_generated_and_serializable()
    {
        var profile = new Profile
        {
            TimeUnixNano = 123456789UL,
            DurationNano = 987654321UL,
            SampleType = new ValueType { TypeStrindex = 1, UnitStrindex = 2 }
        };

        var roundTripped = Profile.Parser.ParseFrom(profile.ToByteArray());

        Assert.Multiple(() =>
        {
            Assert.That(roundTripped.TimeUnixNano, Is.EqualTo(123456789UL));
            Assert.That(roundTripped.DurationNano, Is.EqualTo(987654321UL));
            Assert.That(roundTripped.SampleType.TypeStrindex, Is.EqualTo(1));
            Assert.That(roundTripped.SampleType.UnitStrindex, Is.EqualTo(2));
        });
    }
}
