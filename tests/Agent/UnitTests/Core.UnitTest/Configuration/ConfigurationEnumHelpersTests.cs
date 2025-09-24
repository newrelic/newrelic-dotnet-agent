// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Config;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Configuration
{
    [TestFixture]
    public class ConfigurationEnumHelpersTests
    {
        [Test]
        public void ToRemoteParentSampledBehavior_ValidEnumValues_ReturnsExpectedResults()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RemoteParentSampledBehaviorType.@default.ToRemoteParentSamplerType(), Is.EqualTo(SamplerType.Adaptive));
                Assert.That(RemoteParentSampledBehaviorType.adaptive.ToRemoteParentSamplerType(), Is.EqualTo(SamplerType.Adaptive));
                Assert.That(RemoteParentSampledBehaviorType.alwaysOn.ToRemoteParentSamplerType(), Is.EqualTo(SamplerType.AlwaysOn));
                Assert.That(RemoteParentSampledBehaviorType.alwaysOff.ToRemoteParentSamplerType(), Is.EqualTo(SamplerType.AlwaysOff));
                Assert.That(RemoteParentSampledBehaviorType.traceIdRatioBased.ToRemoteParentSamplerType(), Is.EqualTo(SamplerType.TraceIdRatioBased));
            });
        }

        [Test]
        public void ToRemoteParentSampledBehavior_InvalidEnumValue_ThrowsArgumentOutOfRangeException()
        {
            var invalidValue = (RemoteParentSampledBehaviorType)999;
            Assert.Throws<ArgumentOutOfRangeException>(() => invalidValue.ToRemoteParentSamplerType());
        }

        [Test]
        public void ToRemoteParentSampledBehaviorType_ValidEnumValues_ReturnsExpectedResults()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SamplerType.Default.ToRemoteParentSampledBehaviorType(), Is.EqualTo(RemoteParentSampledBehaviorType.adaptive));
                Assert.That(SamplerType.Adaptive.ToRemoteParentSampledBehaviorType(), Is.EqualTo(RemoteParentSampledBehaviorType.adaptive));
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
