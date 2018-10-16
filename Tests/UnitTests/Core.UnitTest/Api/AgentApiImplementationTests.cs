using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Api
{
	[TestFixture]
	public class AgentApiImplementationTests
	{
		private IConfiguration _configuration;
		private IAgentWrapperApi _wrapperApi;
		private IAgentApi _agentApi;

		[SetUp]
		public void Setup()
		{
			_configuration = Mock.Create<IConfiguration>();
			var configurationService = Mock.Create<IConfigurationService>();
			Mock.Arrange(() => configurationService.Configuration).Returns(_configuration);

			_wrapperApi = Mock.Create<IAgentWrapperApi>();

			_agentApi = new AgentApiImplementation(null, null, null, null, null, null, null, null, configurationService, _wrapperApi, null);
		}


		[Test]
		public void GetRequestMetadataShouldBeNullWhenDistributedTracingEnabled()
		{
			//Arrange
			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);

			var transaction = Mock.Create<ITransactionWrapperApi>();

			Mock.Arrange(() => _wrapperApi.CurrentTransactionWrapperApi).Returns(transaction);
			Mock.Arrange(() => transaction.GetRequestMetadata(Arg.IsAny<ISegment>())).Returns(new Dictionary<string, string> { {"X-NewRelic-ID", "Test"} });
			
			//Act
			var result = _agentApi.GetRequestMetadata();

			//Assert
			Assert.IsNull(result);
		}

		[Test]
		public void GetRequestMetadataShouldNotBeNullWhenDistributedTracingDisabled()
		{
			//Arrange
			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(false);

			var transaction = Mock.Create<ITransactionWrapperApi>();

			Mock.Arrange(() => _wrapperApi.CurrentTransactionWrapperApi).Returns(transaction);
			Mock.Arrange(() => transaction.GetRequestMetadata(Arg.IsAny<ISegment>())).Returns(new Dictionary<string, string> { {"X-NewRelic-ID", "Test"} });
			
			//Act
			var result = _agentApi.GetRequestMetadata();

			//Assert
			Assert.IsNotNull(result);
		}

		[Test]
		public void GetResponseMetadataShouldBeNullWhenDistributedTracingEnabled()
		{
			//Arrange
			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);

			var transaction = Mock.Create<ITransactionWrapperApi>();

			Mock.Arrange(() => _wrapperApi.CurrentTransactionWrapperApi).Returns(transaction);
			Mock.Arrange(() => transaction.GetResponseMetadata()).Returns(new Dictionary<string, string> { {"X-NewRelic-App-Data", "Test"} });
			
			//Act
			var result = _agentApi.GetResponseMetadata();

			//Assert
			Assert.IsNull(result);
		}

		[Test]
		public void GetResponseMetadataShouldNotBeNullWhenDistributedTracingDisabled()
		{
			//Arrange
			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(false);

			var transaction = Mock.Create<ITransactionWrapperApi>();

			Mock.Arrange(() => _wrapperApi.CurrentTransactionWrapperApi).Returns(transaction);
			Mock.Arrange(() => transaction.GetResponseMetadata()).Returns(new Dictionary<string, string> { {"X-NewRelic-App-Data", "Test"} });
			
			//Act
			var result = _agentApi.GetResponseMetadata();

			//Assert
			Assert.IsNotNull(result);
		}
	}
}