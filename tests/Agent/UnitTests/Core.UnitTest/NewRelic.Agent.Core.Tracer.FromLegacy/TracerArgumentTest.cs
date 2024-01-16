// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Tracer
{

    [TestFixture]
    public class TracerArgumentTest
    {
        [Test]
        public static void TestTransactionNamingPriority()
        {
            ClassicAssert.AreEqual(TransactionNamePriority.Handler, TracerArgument.GetTransactionNamingPriority(0x000012F | (3 << 24)));
            ClassicAssert.AreEqual((TransactionNamePriority)7, TracerArgument.GetTransactionNamingPriority(0x0000076 | (7 << 24)));
        }
    }
}
