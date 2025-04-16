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
                Assert.That(RemoteParentSampledBehaviorType.@default.ToRemoteParentSampledBehavior(), Is.EqualTo(RemoteParentSampledBehavior.Default));
                Assert.That(RemoteParentSampledBehaviorType.alwaysOn.ToRemoteParentSampledBehavior(), Is.EqualTo(RemoteParentSampledBehavior.AlwaysOn));
                Assert.That(RemoteParentSampledBehaviorType.alwaysOff.ToRemoteParentSampledBehavior(), Is.EqualTo(RemoteParentSampledBehavior.AlwaysOff));
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
                Assert.That(RemoteParentSampledBehavior.Default.ToRemoteParentSampledBehaviorType(), Is.EqualTo(RemoteParentSampledBehaviorType.@default));
                Assert.That(RemoteParentSampledBehavior.AlwaysOn.ToRemoteParentSampledBehaviorType(), Is.EqualTo(RemoteParentSampledBehaviorType.alwaysOn));
                Assert.That(RemoteParentSampledBehavior.AlwaysOff.ToRemoteParentSampledBehaviorType(), Is.EqualTo(RemoteParentSampledBehaviorType.alwaysOff));
            });
        }

        [Test]
        public void ToRemoteParentSampledBehaviorType_InvalidEnumValue_ThrowsArgumentOutOfRangeException()
        {
            var invalidValue = (RemoteParentSampledBehavior)999;
            Assert.Throws<ArgumentOutOfRangeException>(() => invalidValue.ToRemoteParentSampledBehaviorType());
        }
    }
}
