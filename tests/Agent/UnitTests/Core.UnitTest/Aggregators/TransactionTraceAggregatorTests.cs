// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.TransactionTraces;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.SystemInterfaces;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Aggregators
{
    [TestFixture]
    public class TransactionTraceAggregatorTests
    {
        private TransactionTraceAggregator _transactionTraceAggregator;
        private IDataTransportService _dataTransportService;
        private IEnumerable<ITransactionCollector> _transactionCollectors;
        private ITransactionCollector _transactionCollector1;
        private ITransactionCollector _transactionCollector2;
        private IDnsStatic _dnsStatic;
        private IProcessStatic _processStatic;
        private IScheduler _scheduler;
        private Action _harvestAction;
        private TimeSpan? _harvestCycle;
        private ConfigurationAutoResponder _configurationAutoResponder;

        [SetUp]
        public void SetUp()
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.CollectorSendDataOnExit).Returns(true);
            Mock.Arrange(() => configuration.CollectorSendDataOnExitThreshold).Returns(0);
            Mock.Arrange(() => configuration.TransactionTracerEnabled).Returns(true);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);

            _dataTransportService = Mock.Create<IDataTransportService>();
            _dnsStatic = Mock.Create<IDnsStatic>();
            _processStatic = Mock.Create<IProcessStatic>();

            _transactionCollector1 = Mock.Create<ITransactionCollector>();
            _transactionCollector2 = Mock.Create<ITransactionCollector>();
            _transactionCollectors = new[] { _transactionCollector1, _transactionCollector2 };

            _scheduler = Mock.Create<IScheduler>();
            Mock.Arrange(() => _scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, harvestCycle, __) => { _harvestAction = action; _harvestCycle = harvestCycle; });
            _transactionTraceAggregator = new TransactionTraceAggregator(_dataTransportService, _scheduler, _processStatic, _transactionCollectors);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());
        }

        [TearDown]
        public void TearDown()
        {
            _transactionTraceAggregator.Dispose();
            _configurationAutoResponder.Dispose();
        }

        [Test]
        public void Collect_SendsTraceToAllCollectors()
        {
            var trace = Mock.Create<TransactionTraceWireModelComponents>();

            _transactionTraceAggregator.Collect(trace);

            foreach (var collector in _transactionCollectors)
                Mock.Assert(() => collector.Collect(trace));
        }

        [Test]
        public void Harvest_SendsTracesFromCollectors()
        {
            var sentTraces = Enumerable.Empty<TransactionTraceWireModel>();
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionTraceWireModel>>(), Arg.IsAny<string>()))
                .DoInstead<IEnumerable<TransactionTraceWireModel>>(traces => sentTraces = traces);

            var trace1 = Mock.Create<TransactionTraceWireModel>();
            var trace2 = Mock.Create<TransactionTraceWireModel>();
            Mock.Arrange(() => _transactionCollector1.GetCollectedSamples()).Returns(new[] {
                new TransactionTraceWireModelComponents(new TransactionMetricName(), new TimeSpan(), false, () => trace1) });
            Mock.Arrange(() => _transactionCollector2.GetCollectedSamples()).Returns(new[] {
                new TransactionTraceWireModelComponents(new TransactionMetricName(), new TimeSpan(), false, () => trace2) });

            _harvestAction();

            NrAssert.Multiple(
                () => Assert.That(sentTraces.Count(), Is.EqualTo(2)),
                () => Assert.That(sentTraces, Does.Contain(trace1)),
                () => Assert.That(sentTraces, Does.Contain(trace2))
                );
        }

        [Test]
        public void RetainTransactionTraces_IfServerErrorException()
        {
            var transactionCollector = new SlowestTransactionCollector();
            var transactionCollectors = new[] { transactionCollector };

            var scheduler = Mock.Create<IScheduler>();
            Mock.Arrange(() => scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, _, __) => _harvestAction = action);
            _transactionTraceAggregator = new TransactionTraceAggregator(_dataTransportService, scheduler, _processStatic, transactionCollectors);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

            var sentTraces = Enumerable.Empty<TransactionTraceWireModel>();
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionTraceWireModel>>(), Arg.IsAny<string>()))
                .Returns<IEnumerable<TransactionTraceWireModel>>(traces =>
                {
                    sentTraces = traces;
                    return DataTransportResponseStatus.Retain;
                });

            var trace = new TransactionTraceWireModelComponents(new TransactionMetricName(), TimeSpan.FromSeconds(5), false, () => Mock.Create<TransactionTraceWireModel>());

            _transactionTraceAggregator.Collect(trace);

            _harvestAction();
            sentTraces = Enumerable.Empty<TransactionTraceWireModel>(); //reset
            _harvestAction();

            Assert.That(sentTraces.Count(), Is.EqualTo(1));
        }

        [Test]
        public void PreCleanShutdownEvent_TriggersHarvest()
        {
            var trace = Mock.Create<TransactionTraceWireModel>();
            Mock.Arrange(() => _transactionCollector1.GetCollectedSamples()).Returns(new[] {
                new TransactionTraceWireModelComponents(new TransactionMetricName(), new TimeSpan(), false, () => trace) });

            EventBus<PreCleanShutdownEvent>.Publish(new PreCleanShutdownEvent());

            Mock.Assert(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionTraceWireModel>>(), Arg.IsAny<string>()), Occurs.Once());
        }

        [Test]
        public void When_transaction_traces_disabled_harvest_is_not_scheduled()
        {
            _configurationAutoResponder.Dispose();
            _transactionTraceAggregator.Dispose();
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.TransactionTracerEnabled).Returns(false);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);
            _transactionTraceAggregator = new TransactionTraceAggregator(_dataTransportService, _scheduler, _processStatic, _transactionCollectors);

            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

            Mock.Assert(() => _scheduler.StopExecuting(null, null), Args.Ignore());
        }
    }
}
