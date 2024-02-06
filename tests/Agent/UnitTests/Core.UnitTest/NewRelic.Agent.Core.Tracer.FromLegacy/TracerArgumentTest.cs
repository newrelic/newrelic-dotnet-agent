// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Tracer
{

    [TestFixture]
    public class TracerArgumentTest
    {
        [Test]
        public static void TestTransactionNamingPriority()
        {
            Assert.Multiple(() =>
            {
                Assert.That(TracerArgument.GetTransactionNamingPriority(0x000012F | (3 << 24)), Is.EqualTo(TransactionNamePriority.Handler));
                Assert.That(TracerArgument.GetTransactionNamingPriority(0x0000076 | (7 << 24)), Is.EqualTo((TransactionNamePriority)7));
            });
        }
    }
}
