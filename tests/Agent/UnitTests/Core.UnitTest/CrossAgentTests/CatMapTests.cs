// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using MoreLinq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.BrowserMonitoring;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Synthetics;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemInterfaces;
using NewRelic.Testing.Assertions;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Telerik.JustMock;
using NewRelic.Agent.Api.Experimental;

namespace NewRelic.Agent.Core.CrossAgentTests
{
    //https://source.datanerd.us/newrelic/cross_agent_tests/blob/master/cat/cat_map.json
    [TestFixture]
    public class CatMapTests
    {
        private IConfiguration _configuration;

        private IConfigurationService _configurationService;

        private IPathHashMaker _pathHashMaker;

        private ICatHeaderHandler _catHeaderHandler;

        private ISyntheticsHeaderHandler _syntheticsHeaderHandler;

        private IInternalTransaction _transaction;

        private IAgent _agent;

        private ITransactionAttributeMaker _transactionAttributeMaker;

        private ITransactionMetricNameMaker _transactionMetricNameMaker;

        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc.AttributeDefs;

        [SetUp]
        public void SetUp()
        {
            _configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _configuration.CrossApplicationTracingEnabled).Returns(true);
            _configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => _configurationService.Configuration).Returns(() => _configuration);

            var catSupportabilityCounters = Mock.Create<ICATSupportabilityMetricCounters>();

            _pathHashMaker = new PathHashMaker(_configurationService);
            _catHeaderHandler = new CatHeaderHandler(_configurationService, catSupportabilityCounters);
            _syntheticsHeaderHandler = new SyntheticsHeaderHandler(_configurationService);

            var metricNameService = Mock.Create<IMetricNameService>();
            Mock.Arrange(() => metricNameService.RenameTransaction(Arg.IsAny<TransactionMetricName>()))
                .Returns(name => name);
            _transactionMetricNameMaker = new TransactionMetricNameMaker(metricNameService);

            var transactionBuilderService = Mock.Create<ITransactionService>();
            Mock.Arrange(() => transactionBuilderService.GetCurrentInternalTransaction()).Returns(() => _transaction);

            var agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            var simpleSchedulingService = Mock.Create<ISimpleSchedulingService>();
            var logEventAggregator = Mock.Create<ILogEventAggregator>();
            var logContextDataFilter = Mock.Create<ILogContextDataFilter>();

            _agent = new Agent(transactionBuilderService, Mock.Create<ITransactionTransformer>(), Mock.Create<IThreadPoolStatic>(), _transactionMetricNameMaker, _pathHashMaker, _catHeaderHandler, Mock.Create<IDistributedTracePayloadHandler>(), _syntheticsHeaderHandler, Mock.Create<ITransactionFinalizer>(), Mock.Create<IBrowserMonitoringPrereqChecker>(), Mock.Create<IBrowserMonitoringScriptMaker>(), _configurationService, agentHealthReporter, Mock.Create<IAgentTimerService>(), Mock.Create<IMetricNameService>(), new TraceMetadataFactory(new AdaptiveSampler()), catSupportabilityCounters, logEventAggregator, logContextDataFilter, simpleSchedulingService);

            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
            _transactionAttributeMaker = new TransactionAttributeMaker(_configurationService, _attribDefSvc);
            
        }

        [Test]
        public void JsonCanDeserialize()
        {
            JsonConvert.DeserializeObject<IEnumerable<TestCase>>(JsonTestCaseData);
        }

        [Test]
        [TestCaseSource(typeof(CatMapTests), nameof(TestCases))]
        public void Test(TestCase testCase)
        {
            Mock.Arrange(() => _configuration.ApplicationNames).Returns(new[] { testCase.AppName });

            // ConvertToImmutableTransaction a transaction for the test data
            _transaction = GetTransactionBuilderFor(_configuration, testCase);

            // Simulate external requests as dictated by the test data
            var namePriority = 10;
            var expectedAndActualOutboundRequestPayloads = new Dictionary<CrossApplicationRequestData, CrossApplicationRequestData>();
            testCase.OutboundRequests?.ForEach(request =>
            {
                var transactionName = GetTransactionNameFromString(request.OutboundTxnName);
                _transaction.CandidateTransactionName.TrySet(transactionName, (TransactionNamePriority)namePriority++);
                var outboundHeaders = _agent.CurrentTransaction.GetRequestMetadata().ToDictionary();
                var actualOutboundPayload = _catHeaderHandler.TryDecodeInboundRequestHeaders(outboundHeaders, GetHeaderValue);
                var requestData = new CrossApplicationRequestData(
                    (string)request.ExpectedOutboundPayload[0],
                    (bool)request.ExpectedOutboundPayload[1],
                    (string)request.ExpectedOutboundPayload[2],
                    (string)request.ExpectedOutboundPayload[3]
                );
                expectedAndActualOutboundRequestPayloads.Add(requestData, actualOutboundPayload);
                _transaction.TransactionMetadata.MarkHasCatResponseHeaders();

                List<string> GetHeaderValue(Dictionary<string, string> headers, string key)
                {
                    var headerValues = new List<string>();
                    foreach (var item in headers)
                    {
                        if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                        {
                            headerValues.Add(item.Value);
                        }
                    }
                    return headerValues;
                }
            });

            // Simulate the transaction ending (this logic is normally performed by Agent.EndTransaction)
            _transaction.CandidateTransactionName.TrySet(GetTransactionNameFromString(testCase.TransactionName), (TransactionNamePriority)9999);
            var currentTransactionName = _transaction.CandidateTransactionName.CurrentTransactionName;
            var currentTransactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(currentTransactionName);
            var pathHash = _pathHashMaker.CalculatePathHash(currentTransactionMetricName.PrefixedName, _transaction.TransactionMetadata.CrossApplicationReferrerPathHash);
            _transaction.TransactionMetadata.SetCrossApplicationPathHash(pathHash);
            var transaction = _transaction.ConvertToImmutableTransaction();
            var totalTime = transaction.Duration;

            // Get the attributes that would be created for this transaction
            var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(transaction.TransactionName);
            var txStats = new TransactionMetricStatsCollection(transactionMetricName);
            var attributes = _transactionAttributeMaker.GetAttributes(transaction, transactionMetricName, null, totalTime, txStats);
            var intrinsics = attributes.GetAttributeValuesDic(AttributeClassification.Intrinsics);

            // Run assertions
            testCase.ExpectedIntrinsicFields.ForEach(kvp =>
            {
                Assert.True(intrinsics.ContainsKey(kvp.Key), $"Expected intrinsic attribute '{kvp.Key}' was not found");
                Assert.AreEqual(kvp.Value, intrinsics[kvp.Key], $"Attribute '{kvp.Key}': expected value '{kvp.Value}' but found '{intrinsics[kvp.Key]}'");
            });

            testCase.NonExpectedIntrinsicFields.ForEach(field =>
            {
                Assert.False(intrinsics.ContainsKey(field), $"Found unexpected intrinsic attribute '{field}'");
            });

            if (testCase.OutboundRequests != null)
            {
                expectedAndActualOutboundRequestPayloads.ForEach(kvp =>
                {
                    var expected = kvp.Key;
                    var actual = kvp.Value;
                    Assert.NotNull(actual, "Outbound request did not have any CAT headers");
                    Assert.AreEqual(expected.TransactionGuid, actual.TransactionGuid, $"Expected outbound.TransactionGuid to be '{expected.TransactionGuid}' but found '{actual.TransactionGuid}'");
                    Assert.AreEqual(expected.PathHash, actual.PathHash, $"Expected outbound.PathHash to be '{expected.TransactionGuid}' but found '{actual.TransactionGuid}'");
                    Assert.AreEqual(expected.TripId, actual.TripId, $"Expected outbound.TripId to be '{expected.TransactionGuid}' but found '{actual.TransactionGuid}'");
                    Assert.AreEqual(expected.Unused, actual.Unused, $"Expected outbound.Unused to be '{expected.Unused}' but found '{actual.Unused}'");
                });
            }
        }

        private IInternalTransaction GetTransactionBuilderFor(IConfiguration configuration, TestCase testCase)
        {
            var transactionName = GetTransactionNameFromString(testCase.TransactionName);

            var priority = 0.5f;
            var transaction = new Transaction(configuration, transactionName, Mock.Create<ISimpleTimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), Mock.Create<IDatabaseService>(), priority, Mock.Create<IDatabaseStatementParser>(), Mock.Create<IDistributedTracePayloadHandler>(), Mock.Create<IErrorService>(), _attribDefs);

            SetGuid(transaction, testCase.TransactionGuid);

            var inboundPayload = TryGetValidInboundPayload(testCase.InboundPayload);
            if (inboundPayload != null)
            {
                transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash(inboundPayload.PathHash);
                transaction.TransactionMetadata.SetCrossApplicationReferrerTransactionGuid(inboundPayload.TransactionGuid);
                transaction.TransactionMetadata.SetCrossApplicationReferrerTripId(inboundPayload.TripId);

                // Note: the test data does not call out what the inbound cross process ID is, but the notes indicate that it should be set to something (anything):
                // https://source.datanerd.us/newrelic/cross_agent_tests/tree/master/cat
                transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId("");
            }

            return transaction;
        }

        private static CrossApplicationRequestData TryGetValidInboundPayload(List<object> inboundPayload)
        {
            var serialized = JsonConvert.SerializeObject(inboundPayload);
            try
            {
                return JsonConvert.DeserializeObject<CrossApplicationRequestData>(serialized);
            }
            catch
            {
                return null;
            }
        }

        private static ITransactionName GetTransactionNameFromString(string transactionName)
        {
            var transactionNamePieces = transactionName.Split(MetricNames.PathSeparatorChar);
            if (transactionNamePieces.Length < 2)
                throw new TestFailureException($"Invalid transaction name '{transactionName}'");
            if (transactionNamePieces[0] != "WebTransaction")
                throw new TestFailureException($"Don't know how to create a transaction name that starts with {transactionNamePieces[0]}");

            var transactionNameCategory = transactionNamePieces[1];
            var transactionNameTail = string.Join(MetricNames.PathSeparator, transactionNamePieces.Skip(2));
            return TransactionName.ForWebTransaction(transactionNameCategory, transactionNameTail);
        }

        private static void SetGuid(Transaction transaction, string transactionGuid)
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
            [JsonProperty(PropertyName = "name")]
            public readonly string Name;

            [JsonProperty(PropertyName = "appName")]
            public readonly string AppName;

            [JsonProperty(PropertyName = "transactionName")]
            public readonly string TransactionName;

            [JsonProperty(PropertyName = "transactionGuid")]
            public readonly string TransactionGuid;

            [JsonProperty(PropertyName = "inboundPayload")]
            public readonly List<object> InboundPayload;

            [JsonProperty(PropertyName = "expectedIntrinsicFields")]
            public readonly Dictionary<string, string> ExpectedIntrinsicFields;

            [JsonProperty(PropertyName = "nonExpectedIntrinsicFields")]
            public readonly List<string> NonExpectedIntrinsicFields;

            [JsonProperty(PropertyName = "outboundRequests")]
            public readonly List<OutboundRequest> OutboundRequests;

            public class OutboundRequest
            {
                [JsonProperty(PropertyName = "outboundTxnName")]
                public readonly string OutboundTxnName;

                [JsonProperty(PropertyName = "expectedOutboundPayload")]
                public readonly object[] ExpectedOutboundPayload;
            }

            public override string ToString()
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

        private const string JsonTestCaseData = @"
[
  {
	""name"": ""new_cat"",
	""appName"": ""testAppName"",
	""transactionName"": ""WebTransaction/Custom/testTxnName"",
	""transactionGuid"": ""9323dc260548ed0e"",
	""inboundPayload"": [
	  ""b854df4feb2b1f06"",
	  false,
	  ""7e249074f277923d"",
	  ""5d2957be""
	],
	""expectedIntrinsicFields"": {
	  ""nr.guid"": ""9323dc260548ed0e"",
	  ""nr.tripId"": ""7e249074f277923d"",
	  ""nr.pathHash"": ""815b96d3"",
	  ""nr.referringTransactionGuid"": ""b854df4feb2b1f06"",
	  ""nr.referringPathHash"": ""5d2957be""
	},
	""nonExpectedIntrinsicFields"": [
	  ""nr.alternatePathHashes""
	]
  },
  {
	""name"": ""new_cat_path_hash_with_leading_zero"",
	""appName"": ""testAppName"",
	""transactionName"": ""WebTransaction/Custom/txn4"",
	""transactionGuid"": ""9323dc260548ed0e"",
	""inboundPayload"": [
	  ""b854df4feb2b1f06"",
	  false,
	  ""7e249074f277923d"",
	  ""5d2957be""
	],
	""expectedIntrinsicFields"": {
	  ""nr.guid"": ""9323dc260548ed0e"",
	  ""nr.tripId"": ""7e249074f277923d"",
	  ""nr.pathHash"": ""0e258e4e"",
	  ""nr.referringTransactionGuid"": ""b854df4feb2b1f06"",
	  ""nr.referringPathHash"": ""5d2957be""
	},
	""nonExpectedIntrinsicFields"": [
	  ""nr.alternatePathHashes""
	]
  },
  {
	""name"": ""new_cat_path_hash_with_unicode_name"",
	""appName"": ""testAppName"",
	""transactionName"": ""WebTransaction/Custom/txn\u221a\u221a\u221a"",
	""transactionGuid"": ""9323dc260548ed0e"",
	""inboundPayload"": [
	  ""b854df4feb2b1f06"",
	  false,
	  ""7e249074f277923d"",
	  ""5d2957be""
	  ],
	""expectedIntrinsicFields"": {
	  ""nr.guid"": ""9323dc260548ed0e"",
	  ""nr.tripId"": ""7e249074f277923d"",
	  ""nr.pathHash"": ""3d015d23"",
	  ""nr.referringTransactionGuid"": ""b854df4feb2b1f06"",
	  ""nr.referringPathHash"": ""5d2957be""
	},
	""nonExpectedIntrinsicFields"": [
	  ""nr.alternatePathHashes""
	  ]
  },
  {
	""name"": ""new_cat_no_referring_payload"",
	""appName"": ""testAppName"",
	""transactionName"": ""WebTransaction/Custom/testTxnName"",
	""transactionGuid"": ""9323dc260548ed0e"",
	""inboundPayload"": null,
	""expectedIntrinsicFields"": {},
	""nonExpectedIntrinsicFields"": [
	  ""nr.guid"",
	  ""nr.pathHash"",
	  ""nr.referringTransactionGuid"",
	  ""nr.referringPathHash"",
	  ""nr.alternatePathHashes""
	]
  },
  {
	""name"": ""new_cat_with_call_out"",
	""appName"": ""testAppName"",
	""transactionName"": ""WebTransaction/Custom/testTxnName"",
	""transactionGuid"": ""9323dc260548ed0e"",
	""inboundPayload"": null,
	""outboundRequests"": [
	  {
		""outboundTxnName"": ""WebTransaction/Custom/testTxnName"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""3b0939af""
		]
	  }
	],
	""expectedIntrinsicFields"": {
	  ""nr.guid"": ""9323dc260548ed0e"",
	  ""nr.tripId"": ""9323dc260548ed0e"",
	  ""nr.pathHash"": ""3b0939af""
	},
	""nonExpectedIntrinsicFields"": [
	  ""nr.referringTransactionGuid"",
	  ""nr.referringPathHash"",
	  ""nr.alternatePathHashes""
	]
  },
  {
	""name"": ""new_cat_with_multiple_calls_out"",
	""appName"": ""testAppName"",
	""transactionName"": ""WebTransaction/Custom/testTxnName"",
	""transactionGuid"": ""9323dc260548ed0e"",
	""inboundPayload"": null,
	""outboundRequests"": [
	  {
		""outboundTxnName"": ""WebTransaction/Custom/otherTxnName"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""f1c8adf5""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/otherTxnName"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""f1c8adf5""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/moreOtherTxnName"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""ea19b61c""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/moreDifferentTxnName"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""e00736cc""
		]
	  }
	],
	""expectedIntrinsicFields"": {
	  ""nr.guid"": ""9323dc260548ed0e"",
	  ""nr.tripId"": ""9323dc260548ed0e"",
	  ""nr.pathHash"": ""3b0939af"",
	  ""nr.alternatePathHashes"": ""e00736cc,ea19b61c,f1c8adf5""
	},
	""nonExpectedIntrinsicFields"": [
	  ""nr.referringTransactionGuid"",
	  ""nr.referringPathHash""
	]
  },
  {
	""name"": ""new_cat_with_many_unique_calls_out"",
	""appName"": ""testAppName"",
	""transactionName"": ""WebTransaction/Custom/testTxnName"",
	""transactionGuid"": ""9323dc260548ed0e"",
	""inboundPayload"": null,
	""outboundRequests"": [
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn1"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""93fb4310""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn2"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""a67c2da4""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn3"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""0d932b2b""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn4"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""b4772132""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn5"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""51a1a337""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn6"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""77b5cb70""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn7"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""8a842c7f""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn8"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""b968edb8""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn9"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""2691f90e""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn10"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""b46aec87""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn11"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""10bb3bf3""
		]
	  }
	],
	""expectedIntrinsicFields"": {
	  ""nr.guid"": ""9323dc260548ed0e"",
	  ""nr.tripId"": ""9323dc260548ed0e"",
	  ""nr.pathHash"": ""3b0939af"",
	  ""nr.alternatePathHashes"": ""0d932b2b,2691f90e,51a1a337,77b5cb70,8a842c7f,93fb4310,a67c2da4,b46aec87,b4772132,b968edb8""
	},
	""nonExpectedIntrinsicFields"": [
	  ""nr.referringTransactionGuid"",
	  ""nr.referringPathHash""
	]
  },
  {
	""name"": ""new_cat_with_many_calls_out"",
	""appName"": ""testAppName"",
	""transactionName"": ""WebTransaction/Custom/testTxnName"",
	""transactionGuid"": ""9323dc260548ed0e"",
	""inboundPayload"": null,
	""outboundRequests"": [
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn1"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""93fb4310""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn1"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""93fb4310""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn1"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""93fb4310""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn1"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""93fb4310""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn1"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""93fb4310""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn1"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""93fb4310""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn1"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""93fb4310""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn1"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""93fb4310""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn1"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""93fb4310""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn1"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""93fb4310""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn1"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""93fb4310""
		]
	  },
	  {
		""outboundTxnName"": ""WebTransaction/Custom/txn2"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""9323dc260548ed0e"",
		  ""a67c2da4""
		]
	  }
	],
	""expectedIntrinsicFields"": {
	  ""nr.guid"": ""9323dc260548ed0e"",
	  ""nr.tripId"": ""9323dc260548ed0e"",
	  ""nr.pathHash"": ""3b0939af"",
	  ""nr.alternatePathHashes"": ""93fb4310,a67c2da4""
	},
	""nonExpectedIntrinsicFields"": [
	  ""nr.referringTransactionGuid"",
	  ""nr.referringPathHash""
	]
  },
  {
	""name"": ""new_cat_with_referring_info_and_call_out"",
	""appName"": ""testAppName"",
	""transactionName"": ""WebTransaction/Custom/testTxnName"",
	""transactionGuid"": ""9323dc260548ed0e"",
	""inboundPayload"": [
	  ""b854df4feb2b1f06"",
	  false,
	  ""7e249074f277923d"",
	  ""5d2957be""
	],
	""outboundRequests"": [
	  {
		""outboundTxnName"": ""WebTransaction/Custom/otherTxnName"",
		""expectedOutboundPayload"": [
		  ""9323dc260548ed0e"",
		  false,
		  ""7e249074f277923d"",
		  ""4b9a0289""
		]
	  }
	],
	""expectedIntrinsicFields"": {
	  ""nr.guid"": ""9323dc260548ed0e"",
	  ""nr.tripId"": ""7e249074f277923d"",
	  ""nr.pathHash"": ""815b96d3"",
	  ""nr.alternatePathHashes"": ""4b9a0289"",
	  ""nr.referringTransactionGuid"": ""b854df4feb2b1f06"",
	  ""nr.referringPathHash"": ""5d2957be""
	},
	""nonExpectedIntrinsicFields"": []
  },
  {
	""name"": ""new_cat_missing_path_hash"",
	""appName"": ""testAppName"",
	""transactionName"": ""WebTransaction/Custom/testTxnName"",
	""transactionGuid"": ""9323dc260548ed0e"",
	""inboundPayload"": [
	  ""b854df4feb2b1f06"",
	  false,
	  ""7e249074f277923d""
	],
	""expectedIntrinsicFields"": {
	  ""nr.guid"": ""9323dc260548ed0e"",
	  ""nr.tripId"": ""7e249074f277923d"",
	  ""nr.pathHash"": ""3b0939af"",
	  ""nr.referringTransactionGuid"": ""b854df4feb2b1f06""
	},
	""nonExpectedIntrinsicFields"": [
	  ""nr.alternatePathHashes"",
	  ""nr.referringPathHash""
	]
  },
  {
	""name"": ""new_cat_null_path_hash"",
	""appName"": ""testAppName"",
	""transactionName"": ""WebTransaction/Custom/testTxnName"",
	""transactionGuid"": ""9323dc260548ed0e"",
	""inboundPayload"": [
	  ""b854df4feb2b1f06"",
	  false,
	  ""7e249074f277923d"",
	  null
	],
	""expectedIntrinsicFields"": {
	  ""nr.guid"": ""9323dc260548ed0e"",
	  ""nr.tripId"": ""7e249074f277923d"",
	  ""nr.pathHash"": ""3b0939af"",
	  ""nr.referringTransactionGuid"": ""b854df4feb2b1f06""
	},
	""nonExpectedIntrinsicFields"": [
	  ""nr.alternatePathHashes"",
	  ""nr.referringPathHash""
	]
  },
  {
	""name"": ""new_cat_malformed_path_hash"",
	""appName"": ""testAppName"",
	""transactionName"": ""WebTransaction/Custom/testTxnName"",
	""transactionGuid"": ""9323dc260548ed0e"",
	""inboundPayload"": [
	  ""b854df4feb2b1f06"",
	  false,
	  ""7e249074f277923d"",
	  [
		""scrambled"",
		""eggs""
	  ]
	],
	""expectedIntrinsicFields"": {},
	""nonExpectedIntrinsicFields"": [
	  ""nr.guid"",
	  ""nr.pathHash"",
	  ""nr.referringTransactionGuid"",
	  ""nr.referringPathHash"",
	  ""nr.alternatePathHashes""
	]
  },
  {
	""name"": ""new_cat_corrupt_path_hash"",
	""appName"": ""testAppName"",
	""transactionName"": ""WebTransaction/Custom/testTxnName"",
	""transactionGuid"": ""9323dc260548ed0e"",
	""inboundPayload"": [
	  ""b854df4feb2b1f06"",
	  false,
	  ""7e249074f277923d"",
	  ""ZXYQEDABC""
	],
	""expectedIntrinsicFields"": {
	  ""nr.guid"": ""9323dc260548ed0e"",
	  ""nr.tripId"": ""7e249074f277923d"",
	  ""nr.pathHash"": ""3b0939af"",
	  ""nr.referringTransactionGuid"": ""b854df4feb2b1f06"",
	  ""nr.referringPathHash"": ""ZXYQEDABC""
	},
	""nonExpectedIntrinsicFields"": [
	  ""nr.alternatePathHashes""
	]
  },
  {
	""name"": ""new_cat_malformed_trip_id"",
	""appName"": ""testAppName"",
	""transactionName"": ""WebTransaction/Custom/testTxnName"",
	""transactionGuid"": ""9323dc260548ed0e"",
	""inboundPayload"": [
	  ""b854df4feb2b1f06"",
	  false,
	  [""scrambled""],
	  ""5d2957be""
	],
	""expectedIntrinsicFields"": {},
	""nonExpectedIntrinsicFields"": [
	  ""nr.guid"",
	  ""nr.pathHash"",
	  ""nr.referringTransactionGuid"",
	  ""nr.referringPathHash"",
	  ""nr.alternatePathHashes""
	]
  },
  {
	""name"": ""new_cat_missing_trip_id"",
	""appName"": ""testAppName"",
	""transactionName"": ""WebTransaction/Custom/testTxnName"",
	""transactionGuid"": ""9323dc260548ed0e"",
	""inboundPayload"": [
	  ""b854df4feb2b1f06"",
	  false
	],
	""expectedIntrinsicFields"": {
	  ""nr.guid"": ""9323dc260548ed0e"",
	  ""nr.tripId"": ""9323dc260548ed0e"",
	  ""nr.pathHash"": ""3b0939af"",
	  ""nr.referringTransactionGuid"": ""b854df4feb2b1f06""
	},
	""nonExpectedIntrinsicFields"": [
	  ""nr.referringPathHash"",
	  ""nr.alternatePathHashes""
	]
  },
  {
	""name"": ""new_cat_null_trip_id"",
	""appName"": ""testAppName"",
	""transactionName"": ""WebTransaction/Custom/testTxnName"",
	""transactionGuid"": ""9323dc260548ed0e"",
	""inboundPayload"": [
	  ""b854df4feb2b1f06"",
	  false,
	  null
	],
	""expectedIntrinsicFields"": {
	  ""nr.guid"": ""9323dc260548ed0e"",
	  ""nr.tripId"": ""9323dc260548ed0e"",
	  ""nr.pathHash"": ""3b0939af"",
	  ""nr.referringTransactionGuid"": ""b854df4feb2b1f06""
	},
	""nonExpectedIntrinsicFields"": [
	  ""nr.alternatePathHashes"",
	  ""nr.referringPathHash""
	]
  }
]
";

        #endregion JSON test case data
    }
}
