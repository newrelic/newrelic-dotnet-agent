using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Telerik.JustMock;
using ITransaction = NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders.ITransaction;

namespace NewRelic.Agent.Core.DistributedTracing
{
	[TestFixture]
	public class DistirbutedTracePayloadTests
	{
		private const string _type = "HTTP";
		private const string _accountId = "56789";
		private const string _appId = "12345";
		private const string _guid = "12345";
		private const string _traceId = "12345";
		private const string _trustKey = "12345";
		private const float _priority = .5f;
		private const bool _sampled = true;
		private static DateTime _timestamp = DateTime.UtcNow;
		private const string _transactionId = "12345";

		private DistributedTracePayloadHandler _distributedTracePayloadHandler;
		private IConfiguration _configuration;
		private IAdaptiveSampler _adaptiveSampler;
		private IAgentHealthReporter _agentHealthReporter;

	    [SetUp]
	    public void Setup()
	    {
		    _configuration = Mock.Create<IConfiguration>();
			_adaptiveSampler = Mock.Create<IAdaptiveSampler>();

			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);
			Mock.Arrange(() => _configuration.TransactionEventsEnabled).Returns(true);
			Mock.Arrange(() => _configuration.AccountId).Returns(_accountId);
			Mock.Arrange(() => _configuration.PrimaryApplicationId).Returns(_appId);
			Mock.Arrange(() => _configuration.TrustedAccountKey).Returns(_trustKey);

			var configurationService = Mock.Create<IConfigurationService>();
			Mock.Arrange(() => configurationService.Configuration).Returns(_configuration);
			
			_agentHealthReporter = Mock.Create<IAgentHealthReporter>();
			_distributedTracePayloadHandler = new DistributedTracePayloadHandler(configurationService, _agentHealthReporter, _adaptiveSampler);
	    }

	    [TestCase(null, _accountId, _appId, _traceId)]
	    [TestCase(_type, null, _appId, _traceId)]
	    [TestCase(_type, _accountId, null, _traceId)]
	    [TestCase(_type, _accountId, _appId, null)]
	    [TestCase("", _accountId, _appId, _traceId)]
	    [TestCase(_type, "", _appId, _traceId)]
	    [TestCase(_type, _accountId, "", _traceId)]
	    [TestCase(_type, _accountId, _appId, "")]
	    public void BuildOutgoingPayload_ReturnsNull_WhenRequiredFieldsNotPresent(string type, string accountId, string appId, string traceId)
	    {
			var payload = DistributedTracePayload.TryBuildOutgoingPayload(type, accountId, appId, _guid, traceId, _trustKey, _priority, _sampled, _timestamp, _transactionId);
		    Assert.Null(payload);
	    }

	    [TestCase(null, null)]
	    [TestCase("", "")]
	    public void BuildOutgoingPayload_ReturnsNull_WhenDoesNotContainGuidOrTransactionId(string guid, string transactionId)
	    {
		    var payload = DistributedTracePayload.TryBuildOutgoingPayload(_type, _accountId, _appId, guid, _traceId,
			    _trustKey, _priority, _sampled, _timestamp, transactionId);
			Assert.Null(payload);
	    }

		[Test]
		public void BuildIncomingPayloadFromJson_ReturnsNull_WhenNeitherGuidOrTransactionIdSet()
		{
			var encodedPayload = new Dictionary<string ,string>(){ { "newrelic", "eyJ2IjpbMCwxXSwiZCI6eyJ0eSI6IkFwcCIsImFjIjoiOTEyMyIsImFwIjoiNTE0MjQiLCJ0ciI6IjMyMjFiZjA5YWEwYmNmMGQiLCJwciI6MC4xMjM0LCJzYSI6ZmFsc2UsInRpIjoxNDgyOTU5NTI1NTc3fX0="}};
			var distributedTracePayload = _distributedTracePayloadHandler.TryDecodeInboundRequestHeaders(encodedPayload);

			Mock.Assert(() => _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadParseException(), Occurs.Once());
			Assert.Null(distributedTracePayload);
		}
    }
}
