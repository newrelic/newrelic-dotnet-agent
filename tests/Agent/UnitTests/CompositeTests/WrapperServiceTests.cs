// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using NUnit.Framework;

namespace CompositeTests
{
    [TestFixture]
    public class WrapperServiceTests
    {
        private static CompositeTestAgent _compositeTestAgent;
        private IAgent _agent;

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent(false, true);
            _agent = _compositeTestAgent.GetAgent();
        }

        [TearDown]
        public static void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        [Test]
        public void BeforeWrappedMethod_ReturnsNoOp_IfTheRequiredTransactionIsFinished()
        {
            var transaction = _agent.CreateTransaction(true, "category", "name", true);
            transaction.End();
            _compositeTestAgent.SetTransactionOnPrimaryContextStorage(transaction);

            var type = typeof(WrapperServiceTests);
            var methodName = "MyMethod";
            var tracerFactoryName = "NewRelic.Agent.Core.Wrapper.DefaultWrapper";
            var target = new object();
            var arguments = new object[0];

            using (var logging = new Logging())
            {
                var wrapperService = _compositeTestAgent.GetWrapperService();
                var afterWrappedMethod = wrapperService.BeforeWrappedMethod(type, methodName, string.Empty, target, arguments, tracerFactoryName, null, 0, 0);

                Assert.Multiple(() =>
                {
                    Assert.That(afterWrappedMethod, Is.EqualTo(Delegates.NoOp), "AfterWrappedMethod was not the NoOp delegate.");
                    Assert.That(logging.HasMessageThatContains("Transaction has already ended, skipping method"), Is.True, "Expected log message was not found.");
                });
            }
        }
    }
}
