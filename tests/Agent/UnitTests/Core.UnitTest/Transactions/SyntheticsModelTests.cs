// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core;

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

            ClassicAssert.IsNull(syntheticsData);
        }

        [Test]
        public void TryCreate_ReturnsNull_IfAccountIdIsNotTrusted()
        {
            var obfuscatedHeader = Strings.Base64Encode("[1, 2, \"3\", \"4\", \"5\"]", EncodingKey);
            var trustedAccountIds = new List<long> { 1, 3 };

            var syntheticsData = SyntheticsHeader.TryCreate(trustedAccountIds, obfuscatedHeader, EncodingKey);

            ClassicAssert.IsNull(syntheticsData);
        }

        [Test]
        [TestCase("[1, 2, \"3\", \"4\", \"5\"]", Description = "Normal, valid header")]
        public void TryCreate_ReturnsData_IfHeaderContainsValidData(string header)
        {
            var obfuscatedHeader = Strings.Base64Encode(header, EncodingKey);
            var trustedAccountIds = new List<long> { 1, 2, 3 };

            var syntheticsData = SyntheticsHeader.TryCreate(trustedAccountIds, obfuscatedHeader, EncodingKey);

            NrAssert.Multiple(
                () => ClassicAssert.IsNotNull(syntheticsData),
                () => ClassicAssert.AreEqual(1, syntheticsData.Version),
                () => ClassicAssert.AreEqual(2, syntheticsData.AccountId),
                () => ClassicAssert.AreEqual("3", syntheticsData.ResourceId),
                () => ClassicAssert.AreEqual("4", syntheticsData.JobId),
                () => ClassicAssert.AreEqual("5", syntheticsData.MonitorId)
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
            ClassicAssert.AreEqual("[1,2,\"3\",\"4\",\"5\"]", deobfuscatedHeader);
        }

        #endregion

    }
}
