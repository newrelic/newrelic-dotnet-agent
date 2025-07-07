// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Helpers;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Helpers
{
    [TestFixture]
    public class DictionaryHelpersTests
    {
        // Example: "{\"Host\":\"localhost:7071\",\"traceparent\":\"00-8141368177692588f683b7e7ce8db2a7-bba89a27c8c69cb0-00\"}"
        [TestCase("{\"Host\":\"localhost:7071\",\"traceparent\":\"00-8141368177692588f683b7e7ce8db2a7-bba89a27c8c69cb0-00\"}")]
        [TestCase("{\"Host\":42,\"traceparent\":{\"Test\":\"Code\"}}")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("42")]
        public void Successfuly_Returns_Dictionary_No_Errors(string test)
        {
            var result = DictionaryHelpers.FromJson(test);

            Assert.That(result, Is.Not.Null);

            if (!string.IsNullOrEmpty(test) && test.Contains("traceparent"))
            {
                Assert.That(result["traceparent"], Is.Not.Null);
            }
        }
    }
}
