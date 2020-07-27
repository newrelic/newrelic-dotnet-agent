using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Testing.Assertions;
using Newtonsoft.Json;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.CrossAgentTests
{
    //https://source.datanerd.us/newrelic/cross_agent_tests/blob/master/cat/path_hashing.json
    [TestFixture]
    public class PathHashTests
    {
        [NotNull]
        private IConfiguration _configuration;

        [NotNull]
        private IPathHashMaker _pathHashMaker;

        [SetUp]
        public void SetUp()
        {
            _configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _configuration.CrossApplicationTracingEnabled).Returns(true);
            var configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => configurationService.Configuration).Returns(() => _configuration);

            _pathHashMaker = new PathHashMaker(configurationService);
        }

        [Test]
        public void JsonCanDeserialize()
        {
            JsonConvert.DeserializeObject<IEnumerable<TestCase>>(JsonTestCaseData);
        }

        [Test]
        [TestCaseSource(typeof(PathHashTests), nameof(TestCases))]
        public void Test([NotNull] TestCase testCase)
        {
            Mock.Arrange(() => _configuration.ApplicationNames).Returns(new[] { testCase.ApplicationName });

            var newPathHash = _pathHashMaker.CalculatePathHash(testCase.TransactionName, testCase.ReferringPathHash);

            Assert.AreEqual(testCase.ExpectedPathHash, newPathHash);
        }

        [NotNull]
        private static ITransactionName GetTransactionNameFromString([NotNull] String transactionName)
        {
            var transactionNamePieces = transactionName.Split(MetricNames.PathSeparatorChar);
            if (transactionNamePieces.Length < 2)
                throw new TestFailureException($"Invalid transaction name '{transactionName}'");
            if (transactionNamePieces[0] != "WebTransaction")
                throw new TestFailureException($"Don't know how to create a transaction name that starts with {transactionNamePieces[0]}");

            var transactionNameCategory = transactionNamePieces[1];
            var transactionNameTail = String.Join(MetricNames.PathSeparator, transactionNamePieces.Skip(2));
            return new WebTransactionName(transactionNameCategory, transactionNameTail);
        }

        private static void SetGuid(Transaction transaction, String transactionGuid)
        {
            // We have to set the guid via reflection because it is set up as an auto-generated value in Transaction
            var fieldInfo = typeof(Transaction).GetField("_guid", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldInfo == null)
                throw new NullReferenceException(nameof(fieldInfo));

            fieldInfo.SetValue(transaction, transactionGuid);
        }

        #region JSON test case data
        public class TestCase
        {
            [JsonProperty(PropertyName = "name"), NotNull, UsedImplicitly]
            public readonly String Name;
            [JsonProperty(PropertyName = "applicationName"), NotNull, UsedImplicitly]
            public readonly String ApplicationName;
            [JsonProperty(PropertyName = "transactionName"), NotNull, UsedImplicitly]
            public readonly String TransactionName;
            [JsonProperty(PropertyName = "referringPathHash"), CanBeNull, UsedImplicitly]
            public readonly String ReferringPathHash;
            [JsonProperty(PropertyName = "expectedPathHash"), NotNull, UsedImplicitly]
            public readonly String ExpectedPathHash;

            public override String ToString()
            {
                return Name;
            }
        }

        public static IEnumerable<TestCase[]> TestCases
        {
            get
            {
                var testCases = JsonConvert.DeserializeObject<IEnumerable<TestCase>>(JsonTestCaseData);
                Assert.NotNull(testCases);
                return testCases
                    .Where(testCase => testCase != null)
                    .Select(testCase => new[] { testCase });
            }
        }

        private const String JsonTestCaseData = @"
[
  {
    ""name"": ""no referring path hash"",
    ""referringPathHash"": null,
    ""applicationName"": ""application A"",
    ""transactionName"": ""transaction A"",
    ""expectedPathHash"": ""5e17050e""
  },
  {
    ""name"": ""leading zero on resulting path hash"",
    ""referringPathHash"": null,
    ""applicationName"": ""my application"",
    ""transactionName"": ""transaction 13"",
    ""expectedPathHash"": ""097ca5e1""
  },
  {
    ""name"": ""with referring path hash"",
    ""referringPathHash"": ""95f2f716"",
    ""applicationName"": ""app2"",
    ""transactionName"": ""txn2"",
    ""expectedPathHash"": ""ef72c2e6""
  },
  {
    ""name"": ""with referring path hash leading zero"",
    ""referringPathHash"": ""077634eb"",
    ""applicationName"": ""app3"",
    ""transactionName"": ""txn3"",
    ""expectedPathHash"": ""bfd6587f""
  },
  {
    ""name"": ""with multi-byte UTF-8 characters in transaction name"",
    ""referringPathHash"": ""95f2f716"",
    ""applicationName"": ""app1"",
    ""transactionName"": ""Доверяй, но проверяй"",
    ""expectedPathHash"": ""b7ad900e""
  },
  {
    ""name"": ""high bit of referringPathHash set"",
    ""referringPathHash"": ""80000000"",
    ""applicationName"": ""app1"",
    ""transactionName"": ""txn1"",
    ""expectedPathHash"": ""95f2f717""
  },
  {
    ""name"": ""low bit of referringPathHash set"",
    ""referringPathHash"": ""00000001"",
    ""applicationName"": ""app1"",
    ""transactionName"": ""txn1"",
    ""expectedPathHash"": ""95f2f714""
  }
]
";

        #endregion JSON test case data
    }
}
