// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core
{

    [TestFixture]
    public class SingletonTest
    {
        [Test]
        public static void TestReentrant()
        {
            ClassicAssert.IsTrue(MockAgent.Instance.Enabled);
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
                    ClassicAssert.IsFalse(instance.Enabled);
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
