// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Attributes;
using System;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Core.DistributedTracing.Samplers;

namespace NewRelic.Agent.Core.Api
{
    [TestFixture]
    public class TraceMetadataTests
    {
        private TraceMetadataFactory _traceMetadataFactory;
        private IConfiguration _configuration;
        private ISamplerService _samplerService;
        private float priority = 0.0f;

        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc.AttributeDefs;

        [SetUp]
        public void Setup()
        {
            _configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);
            Mock.Arrange(() => _configuration.TransactionEventsEnabled).Returns(true);

            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));

            _samplerService = Mock.Create<ISamplerService>();
            _traceMetadataFactory = new TraceMetadataFactory(_samplerService);
        }

        [TearDown]
        public void TearDown()
        {
            _attribDefSvc.Dispose();
        }

        [Test]
        public void TraceMetadata_ComputesSampledIfNotSet()
        {
            var transaction = new Transaction(_configuration, Mock.Create<ITransactionName>(), Mock.Create<ISimpleTimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), Mock.Create<IDatabaseService>(), priority, Mock.Create<IDatabaseStatementParser>(), Mock.Create<IDistributedTracePayloadHandler>(), Mock.Create<IErrorService>(), _attribDefs);
            Assert.That(transaction.Sampled, Is.Null);

            Mock.Arrange(() => _samplerService.GetSampler(SamplerLevel.Root).ShouldSample(Arg.IsAny<ISamplingParameters>())).Returns(new SamplingResult(true, priority));   

            var traceMetadata = _traceMetadataFactory.CreateTraceMetadata(transaction);
            var sampled = traceMetadata.IsSampled;

            Assert.That(sampled, Is.EqualTo(true), "TraceMetadata did not set IsSampled.");
            Mock.Assert(() => _samplerService.GetSampler(SamplerLevel.Root).ShouldSample(Arg.IsAny<ISamplingParameters>()), Occurs.Once());
        }

        [Test]
        public void TraceMetadata_ReturnsExistingSampledIfSet()
        {
            var transaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => transaction.Sampled).Returns(true);

            Mock.Arrange(() => _samplerService.GetSampler(SamplerLevel.Root).ShouldSample(Arg.IsAny<ISamplingParameters>())).Returns(new SamplingResult(false, priority));

            var traceMetadata = _traceMetadataFactory.CreateTraceMetadata(transaction);
            var sampled = traceMetadata.IsSampled;

            Assert.That(sampled, Is.EqualTo(true), "TraceMetadata did not use existing Sampled setting.");
            Mock.Assert(() => _samplerService.GetSampler(SamplerLevel.Root).ShouldSample(Arg.IsAny<ISamplingParameters>()), Occurs.Never());
        }
    }
}
