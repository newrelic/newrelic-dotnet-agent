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
        var p = new Profile();
        Assert.That(p.ToByteArray(), Is.Not.Null); // Google.Protobuf message compiles + serializes
    }
}
