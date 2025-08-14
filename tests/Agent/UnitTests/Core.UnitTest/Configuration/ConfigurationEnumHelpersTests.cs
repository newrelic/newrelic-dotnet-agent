// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Configuration.Tests
{
    [TestFixture]
    public class ConfigurationEnumHelpersTests
    {
        [Test]
        public void ToRemoteParentSampledBehavior_ValidEnumValues_ReturnsExpectedResults()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RemoteParentSampledBehaviorType.@default.ToRemoteParentSampledBehavior(), Is.EqualTo(SamplerType.Default));
                Assert.That(RemoteParentSampledBehaviorType.alwaysOn.ToRemoteParentSampledBehavior(), Is.EqualTo(SamplerType.AlwaysOn));
                Assert.That(RemoteParentSampledBehaviorType.alwaysOff.ToRemoteParentSampledBehavior(), Is.EqualTo(SamplerType.AlwaysOff));
                Assert.That(RemoteParentSampledBehaviorType.traceIdRatioBased.ToRemoteParentSampledBehavior(), Is.EqualTo(SamplerType.TraceIdRatioBased));
            });
        }

        [Test]
        public void ToRemoteParentSampledBehavior_InvalidEnumValue_ThrowsArgumentOutOfRangeException()
        {
            var invalidValue = (RemoteParentSampledBehaviorType)999;
            Assert.Throws<ArgumentOutOfRangeException>(() => invalidValue.ToRemoteParentSampledBehavior());
        }

        [Test]
        public void ToRemoteParentSampledBehaviorType_ValidEnumValues_ReturnsExpectedResults()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SamplerType.Default.ToRemoteParentSampledBehaviorType(), Is.EqualTo(RemoteParentSampledBehaviorType.@default));
                Assert.That(SamplerType.AlwaysOn.ToRemoteParentSampledBehaviorType(), Is.EqualTo(RemoteParentSampledBehaviorType.alwaysOn));
                Assert.That(SamplerType.AlwaysOff.ToRemoteParentSampledBehaviorType(), Is.EqualTo(RemoteParentSampledBehaviorType.alwaysOff));
                Assert.That(SamplerType.TraceIdRatioBased.ToRemoteParentSampledBehaviorType(), Is.EqualTo(RemoteParentSampledBehaviorType.traceIdRatioBased));
            });
        }

        [Test]
        public void ToRemoteParentSampledBehaviorType_InvalidEnumValue_ThrowsArgumentOutOfRangeException()
        {
            var invalidValue = (SamplerType)999;
            Assert.Throws<ArgumentOutOfRangeException>(() => invalidValue.ToRemoteParentSampledBehaviorType());
        }
    }
}
