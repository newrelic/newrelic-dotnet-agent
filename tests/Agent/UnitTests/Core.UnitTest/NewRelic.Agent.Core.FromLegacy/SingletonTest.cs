// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core
{

    [TestFixture]
    public class SingletonTest
    {
        [Test]
        public static void TestReentrant()
        {
            Assert.That(MockAgent.Instance.Enabled, Is.True);
        }
    }

    interface IAgentMock
    {
        bool Enabled { get; }
    }

    class DisabledMock : IAgentMock
    {

        public bool Enabled
        {
            get { return false; }
        }
    }

    class MockAgent : IAgentMock
    {
        private readonly static MockSingleton singleton = new MockSingleton();
        private class MockSingleton : Singleton<IAgentMock>
        {
            private volatile int count = 0;
            public MockSingleton()
                : base(new DisabledMock())
            {
            }

            protected override IAgentMock CreateInstance()
            {
                if (count == 0)
                {
                    IAgentMock instance = MockAgent.Instance;
                    Assert.That(instance.Enabled, Is.False);
                }

                count++;
                return new MockAgent();
            }
        }
        public static IAgentMock Instance
        {
            get
            {
                try
                {
                    return singleton.ExistingInstance;
                }
                catch (NullReferenceException)
                {
                    return new DisabledMock();
                }
            }
        }

        public bool Enabled
        {
            get { return true; }
        }
    }
}
