// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NewRelic.Agent.Configuration;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing
{
    [TestFixture]
    public class PathHashMakerTests
    {
        private const string AppName = "appName";
        private const string ReferringPathHash = "12345678";

        private PathHashMaker _pathHashMaker;

        private IConfiguration _configuration;

        [SetUp]
        public void SetUp()
        {
            _configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _configuration.ApplicationNames).Returns(new[] { AppName });
            var configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => configurationService.Configuration).Returns(_configuration);

            _pathHashMaker = new PathHashMaker(configurationService);
        }

        [Test]
        public void CalculatePathHash_ReturnsCorrectPathHash_IfReferringPathHashIsNull()
        {
            var pathHash = _pathHashMaker.CalculatePathHash("transactionName", null);
            ClassicAssert.AreEqual("9d743449", pathHash);
        }

        [Test]
        public void CalculatePathHash_ReturnsCorrectPathHash_IfReferringPathHashIsNotNull()
        {
            var pathHash = _pathHashMaker.CalculatePathHash("transactionName", ReferringPathHash);
            ClassicAssert.AreEqual("b91c98b9", pathHash);
        }

        [Test]
        public void CalculatePathHash_ReturnsReversiblePathHash()
        {
            var pathHash = _pathHashMaker.CalculatePathHash("transactionName", ReferringPathHash);
            ClassicAssert.NotNull(pathHash);

            var reversedPathHash = ReversePathHash("transactionName", AppName, pathHash);
            ClassicAssert.AreEqual(ReferringPathHash, reversedPathHash);
        }

        private static string ReversePathHash(string transactionName, string appName, string pathHash)
        {
            var parameters = new object[] { transactionName, appName, pathHash };
            var result = typeof(PathHashMaker).InvokeMember("ReversePathHash", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod, null, null, parameters);
            return (string)result;
        }
    }
}
