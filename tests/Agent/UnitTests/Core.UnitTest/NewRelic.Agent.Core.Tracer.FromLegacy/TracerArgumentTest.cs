// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace NewRelic.Agent.Core.Tracer
{

    [TestFixture]
    public class TracerArgumentTest
    {
        [Test]
        public static void TestTransactionNamingPriority()
        {
            Assert.AreEqual(3, TracerArgument.GetTransactionNamingPriority(0x000012F | (3 << 24)));
            Assert.AreEqual(7, TracerArgument.GetTransactionNamingPriority(0x0000076 | (7 << 24)));
        }
    }
}
