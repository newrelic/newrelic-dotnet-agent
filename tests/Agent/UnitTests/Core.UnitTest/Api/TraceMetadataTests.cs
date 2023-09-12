// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Core.Spans.Tests;
using System;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Errors;

namespace NewRelic.Agent.Core.Api
{
    [TestFixture]
    public class TraceMetadataTests
    {
        private TraceMetadataFactory _traceMetadataFactory;
        private IConfiguration _configuration;
        private IAdaptiveSampler _adaptiveSampler;
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

            _adaptiveSampler = Mock.Create<IAdaptiveSampler>();
            _traceMetadataFactory = new TraceMetadataFactory(_adaptiveSampler);
        }

        [Test]
        public void TraceMetadata_ComputesSampledIfNotSet()
        {
            var transaction = new Transaction(_configuration, Mock.Create<ITransactionName>(), Mock.Create<ISimpleTimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), Mock.Create<IDatabaseService>(), priority, Mock.Create<IDatabaseStatementParser>(), Mock.Create<IDistributedTracePayloadHandler>(), Mock.Create<IErrorService>(), _attribDefs);
            Assert.IsNull(transaction.Sampled);

            Mock.Arrange(() => _adaptiveSampler.ComputeSampled(ref priority)).Returns(true);

            var traceMetadata = _traceMetadataFactory.CreateTraceMetadata(transaction);
            var sampled = traceMetadata.IsSampled;

            Assert.AreEqual(true, sampled, "TraceMetadata did not set IsSampled.");
            Mock.Assert(() => _adaptiveSampler.ComputeSampled(ref Arg.Ref(Arg.AnyFloat).Value), Occurs.Once());
        }

        [Test]
        public void TraceMetadata_ReturnsExistingSampledIfSet()
        {
            var transaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => transaction.Sampled).Returns(true);

            Mock.Arrange(() => _adaptiveSampler.ComputeSampled(ref priority)).Returns(false);

            var traceMetadata = _traceMetadataFactory.CreateTraceMetadata(transaction);
            var sampled = traceMetadata.IsSampled;

            Assert.AreEqual(true, sampled, "TraceMetadata did not use existing Sampled setting.");
            Mock.Assert(() => _adaptiveSampler.ComputeSampled(ref Arg.Ref(Arg.AnyFloat).Value), Occurs.Never());
        }
    }
}
