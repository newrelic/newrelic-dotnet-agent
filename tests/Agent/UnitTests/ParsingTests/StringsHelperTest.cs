// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace NewRelic.Parsing
{

    [TestFixture]
    public class StringsHelperTest
    {
        [Test]
        public void DoubleBracket()
        {
            Assert.AreEqual("dude", StringsHelper.RemoveBracketsQuotesParenthesis("[[dude]]"));
        }
    }
}
