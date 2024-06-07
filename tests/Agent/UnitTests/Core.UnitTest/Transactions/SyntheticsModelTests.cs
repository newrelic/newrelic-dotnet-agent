// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Transactions.UnitTest
{
    [TestFixture]
    public class Class_SyntheticsData
    {
        const string EncodingKey = "TestEncodingKey";

        #region TryCreate

        [Test]
        [TestCase(null, "[1, 2, \"3\", \"4\", \"5\"]", EncodingKey)]
        [TestCase(2, null, EncodingKey)]
        [TestCase(2, "[1, 2, \"3\", \"4\", \"5\"]", null)]
        public void TryCreate_ReturnsNull_IfParameterIsNull(int? accountId, string header, string encodingKey)
        {
            var accountIds = accountId == null ? null : new List<long> { accountId.Value };
            var obfuscatedHeader = header == null ? null : Strings.Base64Encode(header, EncodingKey);

            SyntheticsHeader.TryCreate(accountIds, obfuscatedHeader, encodingKey);
        }

        [Test]
        [TestCase("banana", Description = "Invalid (non-JSON) header")]
        [TestCase("[999, 2, \"3\", \"4\", \"5\"]", Description = "Unsupported version number")]
        [TestCase("[999]", Description = "Unsupported version number and missing all other properties")]
        [TestCase("[\"banana\", 2, \"3\", \"4\", \"5\"]", Description = "Invalid version number")]
        [TestCase("[1, \"banana\", \"3\", \"4\", \"5\"]", Description = "Invalid account ID")]
        [TestCase("[1, 2, \"3\", \"4\", \"5\", 6, \"7\"]", Description = "Header with extra data")]
        [TestCase("[1, 2, \"3\", \"4\"]", Description = "Header with missing extra data")]
        public void TryCreate_ReturnsNull_IfHeaderIsInvalid(string header)
        {
            var obfuscatedHeader = Strings.Base64Encode(header, EncodingKey);
            var trustedAccountIds = new List<long> { 1, 2, 3 };

            var syntheticsData = SyntheticsHeader.TryCreate(trustedAccountIds, obfuscatedHeader, EncodingKey);

            Assert.That(syntheticsData, Is.Null);
        }

        [Test]
        public void TryCreate_ReturnsNull_IfAccountIdIsNotTrusted()
        {
            var obfuscatedHeader = Strings.Base64Encode("[1, 2, \"3\", \"4\", \"5\"]", EncodingKey);
            var trustedAccountIds = new List<long> { 1, 3 };

            var syntheticsData = SyntheticsHeader.TryCreate(trustedAccountIds, obfuscatedHeader, EncodingKey);

            Assert.That(syntheticsData, Is.Null);
        }

        [Test]
        [TestCase("[1, 2, \"3\", \"4\", \"5\"]", Description = "Normal, valid header")]
        public void TryCreate_ReturnsData_IfHeaderContainsValidData(string header)
        {
            var obfuscatedHeader = Strings.Base64Encode(header, EncodingKey);
            var trustedAccountIds = new List<long> { 1, 2, 3 };

            var syntheticsData = SyntheticsHeader.TryCreate(trustedAccountIds, obfuscatedHeader, EncodingKey);

            NrAssert.Multiple(
                () => Assert.That(syntheticsData, Is.Not.Null),
                () => Assert.That(syntheticsData.Version, Is.EqualTo(1)),
                () => Assert.That(syntheticsData.AccountId, Is.EqualTo(2)),
                () => Assert.That(syntheticsData.ResourceId, Is.EqualTo("3")),
                () => Assert.That(syntheticsData.JobId, Is.EqualTo("4")),
                () => Assert.That(syntheticsData.MonitorId, Is.EqualTo("5"))
                );
        }

        #endregion TryCreate

        #region TryGetObfuscated

        [Test]
        public void TryGetObfuscated_ReturnsObfuscatedString()
        {
            var syntheticsHeader = new SyntheticsHeader(1, 2, "3", "4", "5") { EncodingKey = EncodingKey };

            var obfuscatedHeader = syntheticsHeader.TryGetObfuscated();

            var deobfuscatedHeader = Strings.Base64Decode(obfuscatedHeader, EncodingKey);
            Assert.That(deobfuscatedHeader, Is.EqualTo("[1,2,\"3\",\"4\",\"5\"]"));
        }

        #endregion

    }
}
