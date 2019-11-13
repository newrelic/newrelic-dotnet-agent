using JetBrains.Annotations;
using MoreLinq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.BrowserMonitoring;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi;
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

namespace NewRelic.Agent.Core.CrossAgentTests
{
	//https://source.datanerd.us/newrelic/cross_agent_tests/blob/master/cat/cat_map.json
	[TestFixture]
	public class CatMapTests
	{
		[NotNull]
		private IConfiguration _configuration;

		[NotNull]
		private IConfigurationService _configurationService;

		[NotNull]
		private IPathHashMaker _pathHashMaker;

		[NotNull]
		private ICatHeaderHandler _catHeaderHandler;
		
		[NotNull]
		private ISyntheticsHeaderHandler _syntheticsHeaderHandler;

		[NotNull]
		private IInternalTransaction _transaction;

		[NotNull]
		private IAgent _agent;

		[NotNull]
		private ITransactionAttributeMaker _transactionAttributeMaker;

		[NotNull]
		private ITransactionMetricNameMaker _transactionMetricNameMaker;

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

			_agent = new Wrapper.AgentWrapperApi.Agent(transactionBuilderService, Mock.Create<ITimerFactory>(), Mock.Create<ITransactionTransformer>(), Mock.Create<IThreadPoolStatic>(), _transactionMetricNameMaker, _pathHashMaker, _catHeaderHandler, Mock.Create<IDistributedTracePayloadHandler>(), _syntheticsHeaderHandler, Mock.Create<ITransactionFinalizer>(), Mock.Create<IBrowserMonitoringPrereqChecker>(), Mock.Create<IBrowserMonitoringScriptMaker>(), _configurationService, agentHealthReporter, Mock.Create<IAgentTimerService>(), Mock.Create<IMetricNameService>(), new TraceMetadataFactory(new AdaptiveSampler()), catSupportabilityCounters);

			_transactionAttributeMaker = new TransactionAttributeMaker(_configurationService);
		}

		[Test]
		public void JsonCanDeserialize()
		{
			JsonConvert.DeserializeObject<IEnumerable<TestCase>>(JsonTestCaseData);
		}

		[Test]
		[TestCaseSource(typeof (CatMapTests), nameof(TestCases))]
		public void Test([NotNull] TestCase testCase)
		{
			Mock.Arrange(() => _configuration.ApplicationNames).Returns(new[] {testCase.AppName});

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
				var actualOutboundPayload = _catHeaderHandler.TryDecodeInboundRequestHeaders(outboundHeaders);
				var requestData = new CrossApplicationRequestData(
					(string)request.ExpectedOutboundPayload[0],
					(bool)request.ExpectedOutboundPayload[1],
					(string)request.ExpectedOutboundPayload[2],
					(string)request.ExpectedOutboundPayload[3]
				);
				expectedAndActualOutboundRequestPayloads.Add(requestData, actualOutboundPayload);
				_transaction.TransactionMetadata.MarkHasCatResponseHeaders();
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
			var errorData = ErrorData.TryGetErrorData(transaction, _configurationService);
			var attributes = _transactionAttributeMaker.GetAttributes(transaction, transactionMetricName, null, totalTime, errorData, txStats);
			var intrinsics = attributes.GetIntrinsicsDictionary();

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

		private static IInternalTransaction GetTransactionBuilderFor([NotNull] IConfiguration configuration, [NotNull] TestCase testCase)
		{
			var transactionName = GetTransactionNameFromString(testCase.TransactionName);

			var priority = 0.5f;
			var transaction = new Transaction(configuration, transactionName, Mock.Create<ITimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator(), priority, Mock.Create<IDatabaseStatementParser>());

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

		private static CrossApplicationRequestData TryGetValidInboundPayload(List<Object> inboundPayload)
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
			return TransactionName.ForWebTransaction(transactionNameCategory, transactionNameTail);
		}

		private static void SetGuid(Transaction transaction, String transactionGuid)
		{
			// We have to set the guid via reflection because it is set up as an auto-generated value in Transaction
			var fieldInfo = typeof (Transaction).GetField("_guid", BindingFlags.Instance | BindingFlags.NonPublic);
			if (fieldInfo == null)
				throw new NullReferenceException(nameof(fieldInfo));

			fieldInfo.SetValue(transaction, transactionGuid);
		}

		#region JSON test case data

		public class TestCase
		{
			[JsonProperty(PropertyName = "name"), NotNull, UsedImplicitly]
			public readonly String Name;

			[JsonProperty(PropertyName = "appName"), NotNull, UsedImplicitly]
			public readonly String AppName;

			[JsonProperty(PropertyName = "transactionName"), NotNull, UsedImplicitly]
			public readonly String TransactionName;

			[JsonProperty(PropertyName = "transactionGuid"), NotNull, UsedImplicitly]
			public readonly String TransactionGuid;

			[JsonProperty(PropertyName = "inboundPayload"), NotNull, UsedImplicitly]
			public readonly List<Object> InboundPayload;

			[JsonProperty(PropertyName = "expectedIntrinsicFields"), NotNull, UsedImplicitly]
			public readonly Dictionary<String, String> ExpectedIntrinsicFields;

			[JsonProperty(PropertyName = "nonExpectedIntrinsicFields"), NotNull, UsedImplicitly]
			public readonly List<String> NonExpectedIntrinsicFields;

			[JsonProperty(PropertyName = "outboundRequests"), CanBeNull, UsedImplicitly]
			public readonly List<OutboundRequest> OutboundRequests;

			public class OutboundRequest
			{
				[JsonProperty(PropertyName = "outboundTxnName"), NotNull, UsedImplicitly]
				public readonly String OutboundTxnName;

				[JsonProperty(PropertyName = "expectedOutboundPayload"), NotNull, UsedImplicitly]
				public readonly object[] ExpectedOutboundPayload;
			}

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
					.Select(testCase => new[] {testCase});
			}
		}

		private const String JsonTestCaseData = @"
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
