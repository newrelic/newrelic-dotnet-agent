using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.SharedInterfaces;
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
        private Action _harvestAction;
        private ConfigurationAutoResponder _configurationAutoResponder;

        [SetUp]
        public void SetUp()
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.CollectorSendDataOnExit).Returns(true);
            Mock.Arrange(() => configuration.CollectorSendDataOnExitThreshold).Returns(0);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);

            _dataTransportService = Mock.Create<IDataTransportService>();
            _dnsStatic = Mock.Create<IDnsStatic>();
            _processStatic = Mock.Create<IProcessStatic>();

            _transactionCollector1 = Mock.Create<ITransactionCollector>();
            _transactionCollector2 = Mock.Create<ITransactionCollector>();
            _transactionCollectors = new[] { _transactionCollector1, _transactionCollector2 };

            var scheduler = Mock.Create<IScheduler>();
            Mock.Arrange(() => scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, _, __) => _harvestAction = action);
            _transactionTraceAggregator = new TransactionTraceAggregator(_dataTransportService, scheduler, _processStatic, _transactionCollectors);
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
            Mock.Arrange(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionTraceWireModel>>()))
                .DoInstead<IEnumerable<TransactionTraceWireModel>>(traces => sentTraces = traces);

            var trace1 = Mock.Create<TransactionTraceWireModel>();
            var trace2 = Mock.Create<TransactionTraceWireModel>();
            Mock.Arrange(() => _transactionCollector1.GetAndClearCollectedSamples()).Returns(new[] {
                new TransactionTraceWireModelComponents(new TransactionMetricName(), new TimeSpan(), false, () => trace1) });
            Mock.Arrange(() => _transactionCollector2.GetAndClearCollectedSamples()).Returns(new[] {
                new TransactionTraceWireModelComponents(new TransactionMetricName(), new TimeSpan(), false, () => trace2) });

            _harvestAction();

            NrAssert.Multiple(
                () => Assert.AreEqual(2, sentTraces.Count()),
                () => Assert.IsTrue(sentTraces.Contains(trace1)),
                () => Assert.IsTrue(sentTraces.Contains(trace2))
                );
        }

        [Test]
        public void PreCleanShutdownEvent_TriggersHarvest()
        {
            var trace = Mock.Create<TransactionTraceWireModel>();
            Mock.Arrange(() => _transactionCollector1.GetAndClearCollectedSamples()).Returns(new[] {
                new TransactionTraceWireModelComponents(new TransactionMetricName(), new TimeSpan(), false, () => trace) });

            EventBus<PreCleanShutdownEvent>.Publish(new PreCleanShutdownEvent());

            Mock.Assert(() => _dataTransportService.Send(Arg.IsAny<IEnumerable<TransactionTraceWireModel>>()), Occurs.Once());
        }
    }
}
