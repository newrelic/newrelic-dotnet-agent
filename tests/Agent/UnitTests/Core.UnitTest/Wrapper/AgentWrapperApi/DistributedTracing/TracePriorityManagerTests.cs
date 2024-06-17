// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using NewRelic.Agent.Core.DistributedTracing;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.DistributedTracing
{
    [TestFixture]
    public class TracePriorityManagerTests
    {
        private const int SeedForTesting = 6351;
        private const float Epsilon = 99e-7f;
        [Test]
        public void TracePriorityManager_CreateNoSeed()
        {
            var priorityManager = new TracePriorityManager();
            for (var i = 0; i < 50; ++i)
            {
                var priority = priorityManager.Create();
                Assert.That(priority, Is.LessThanOrEqualTo(1.0f).And.GreaterThanOrEqualTo(0.0f));
            }
        }

        private static readonly float[] Expected =
        {
            0.649413f, 0.318965f, 0.040750f, 0.134213f, 0.591966f, 0.683855f, 0.514113f, 0.639820f, 0.702314f, 0.211925f,
            0.267489f, 0.784756f, 0.634333f, 0.610726f, 0.503470f, 0.362579f, 0.562824f, 0.611073f, 0.601204f, 0.452799f,
            0.677696f, 0.981417f, 0.342722f, 0.728304f, 0.134228f, 0.381583f, 0.301303f, 0.817772f, 0.775553f, 0.919870f,
            0.834961f, 0.350054f, 0.186482f, 0.212073f, 0.276135f, 0.002707f, 0.876837f, 0.579997f, 0.964015f, 0.074807f,
            0.142079f, 0.175165f, 0.822640f, 0.978020f, 0.382791f, 0.739000f, 0.082855f, 0.951306f, 0.599572f, 0.628990f
        };

        [Test]
        public void TracePriorityManager_CreateWithSeed()
        {
            var priorityManager = new TracePriorityManager(SeedForTesting);
            foreach (var expect in Expected)
            {
                var priority = priorityManager.Create();
                Assert.That(priority, Is.EqualTo(expect).Within(Epsilon));
            }
        }

        [Test]
        public void TracePriorityManager_Adjust(
            [Values(0.0f, 0.000001f, 1.0f)] float priority,
            [Values(0.0f, 1.0f, 0.5f, 0.000001f)] float adjust
            )
        {
            var adjustedPriority = TracePriorityManager.Adjust(priority, adjust);
            Assert.That(adjustedPriority, Is.EqualTo(priority + adjust).Within(Epsilon));
        }
    }
}
