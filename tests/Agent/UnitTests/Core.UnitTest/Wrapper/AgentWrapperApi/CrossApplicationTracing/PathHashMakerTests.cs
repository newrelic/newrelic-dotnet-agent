using System;
using System.Reflection;
using NewRelic.Agent.Configuration;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing
{
    [TestFixture]
    public class PathHashMakerTests
    {
        private const String AppName = "appName";
        private const String ReferringPathHash = "12345678";
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
            Assert.AreEqual("9d743449", pathHash);
        }

        [Test]
        public void CalculatePathHash_ReturnsCorrectPathHash_IfReferringPathHashIsNotNull()
        {
            var pathHash = _pathHashMaker.CalculatePathHash("transactionName", ReferringPathHash);
            Assert.AreEqual("b91c98b9", pathHash);
        }

        [Test]
        public void CalculatePathHash_ReturnsReversiblePathHash()
        {
            var pathHash = _pathHashMaker.CalculatePathHash("transactionName", ReferringPathHash);
            Assert.NotNull(pathHash);

            var reversedPathHash = ReversePathHash("transactionName", AppName, pathHash);
            Assert.AreEqual(ReferringPathHash, reversedPathHash);
        }

        private static String ReversePathHash(String transactionName, String appName, String pathHash)
        {
            var parameters = new Object[] { transactionName, appName, pathHash };
            var result = typeof(PathHashMaker).InvokeMember("ReversePathHash", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod, null, null, parameters);
            return (String)result;
        }
    }
}
