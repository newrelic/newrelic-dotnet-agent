// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroProcessEnvironmentVariablesTests
{
    private const string Name = "NR_DISTRO_PROCESS_ENV_TEST_VARIABLE";

    [TearDown]
    public void TearDown()
    {
        System.Environment.SetEnvironmentVariable(Name, null);
    }

    [Test]
    public void SetThenGet_RoundTripsThroughTheProcessEnvironment()
    {
        var env = new DistroProcessEnvironmentVariables();

        Assert.That(env.Get(Name), Is.Null);

        env.Set(Name, "false");

        Assert.Multiple(() =>
        {
            Assert.That(env.Get(Name), Is.EqualTo("false"));
            Assert.That(System.Environment.GetEnvironmentVariable(Name), Is.EqualTo("false"));
        });
    }
}
