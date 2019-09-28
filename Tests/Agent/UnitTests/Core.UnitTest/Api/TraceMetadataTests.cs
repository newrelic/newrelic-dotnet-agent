using NewRelic.Agent.Configuration;
using NUnit.Framework;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using Telerik.JustMock;
using NewRelic.Agent.Core.DistributedTracing;

namespace NewRelic.Agent.Core.Api
{
	[TestFixture]
	public class TraceMetadataTests
	{
		private TraceMetadataFactory _traceMetadataFactory;
		private IConfiguration _configuration;
		private IAdaptiveSampler _adaptiveSampler;
		private float priority = 0.0f;
		private IInternalTransaction _transaction;

		[SetUp]
		public void Setup()
		{
			_configuration = Mock.Create<IConfiguration>();
			Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);
			Mock.Arrange(() => _configuration.TransactionEventsEnabled).Returns(true);

			var configurationService = Mock.Create<IConfigurationService>();
			Mock.Arrange(() => configurationService.Configuration).Returns(_configuration);

			_adaptiveSampler = Mock.Create<IAdaptiveSampler>();
			_traceMetadataFactory = new TraceMetadataFactory(_adaptiveSampler);

			_transaction = Mock.Create<IInternalTransaction>();
			Mock.Arrange(() => _transaction.TransactionMetadata).Returns(new TransactionMetadata());
		}

		[Test]
		public void TraceMetadata_ComputesSampledIfNotSet()
		{
			_transaction.TransactionMetadata.DistributedTraceSampled = null;

			Mock.Arrange(() => _adaptiveSampler.ComputeSampled(ref priority)).Returns(true);

			var traceMetadata = _traceMetadataFactory.CreateTraceMetadata(_transaction);
			var sampled = traceMetadata.IsSampled;

			Assert.AreEqual(true, sampled, "TraceMetadata did not set IsSampled.");
			Mock.Assert(() => _adaptiveSampler.ComputeSampled(ref Arg.Ref(Arg.AnyFloat).Value), Occurs.Once());
		}

		[Test]
		public void TraceMetadata_ReturnsExistingSampledIfSet()
		{
			_transaction.TransactionMetadata.DistributedTraceSampled = true;

			Mock.Arrange(() => _adaptiveSampler.ComputeSampled(ref priority)).Returns(false);

			var traceMetadata = _traceMetadataFactory.CreateTraceMetadata(_transaction);
			var sampled = traceMetadata.IsSampled;

			Assert.AreEqual(true, sampled, "TraceMetadata did not use existing Sampled setting.");
			Mock.Assert(() => _adaptiveSampler.ComputeSampled(ref Arg.Ref(Arg.AnyFloat).Value), Occurs.Never());
		}
	}
}
