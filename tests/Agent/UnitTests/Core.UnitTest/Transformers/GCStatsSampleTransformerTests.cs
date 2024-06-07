// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.WireModels;
using NUnit.Framework;
using Telerik.JustMock;
using static NewRelic.Agent.Core.WireModels.MetricWireModel;

namespace NewRelic.Agent.Core.Transformers
{
    [TestFixture]
    public class GCStatsSampleTransformerTests
    {
        private IGcSampleTransformer _transformer;

        private IMetricBuilder _metricBuilder;

        private IMetricAggregator _metricAggregator;

        private GCSampleType[] _sampleTypes;
        private int _sampleTypesCount;

        private Dictionary<GCSampleType, float> _sampleData;


        [SetUp]
        public void Setup()
        {
            var metricNameService = new MetricNameService();
            _metricBuilder = new MetricBuilder(metricNameService);
            _metricAggregator = Mock.Create<IMetricAggregator>();

            _transformer = new GcSampleTransformer(_metricBuilder, _metricAggregator);

            //Build example sample data
            var sampleValue = 0f;
            _sampleData = new Dictionary<GCSampleType, float>();
            foreach (var val in Enum.GetValues(typeof(GCSampleType)))
            {
                _sampleData.Add((GCSampleType)val, sampleValue++);
            }
            _sampleTypes = _sampleData.Keys.ToArray();
            _sampleTypesCount = _sampleTypes.Length;
        }

        /// <summary>
        /// Ensures that our transformer can create a metric for each sample type that is defined in the enum.
        /// </summary>
        [Test]
        public void CanGenerateMetricForAllSampleTypes()
        {
            //Collect metrics that are generated from sample data
            var generatedMetrics = new Dictionary<string, MetricWireModel>();
            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<MetricWireModel>()))
                .DoInstead<MetricWireModel>((metric) => { generatedMetrics.Add(metric.MetricNameModel.Name, metric); });

            //Act
            _transformer.Transform(_sampleData);

            //Assert
            Assert.That(generatedMetrics, Has.Count.EqualTo(_sampleTypesCount), $"{_sampleTypesCount} metrics should have been generated, but {generatedMetrics.Count} were.");
            Assert.Multiple(() =>
            {
                Assert.That(generatedMetrics.Any(x => x.Value == null), Is.False);
                Assert.That(generatedMetrics.Any(x => x.Value.DataModel == null), Is.False);
            });
        }

        /// <summary>
        /// Ensures that the transformer generates the proper type of metric for each sample type.
        /// </summary>
        [Test]
        public void TransformerGeneratesCorrectMetricTypesForEachSampleType()
        {
            var metricNameService = new MetricNameService();
            var metricBuilder = Mock.Create<IMetricBuilder>();

            //Accumulate the GCSampleTypes that generated each metric type.
            var sampleTypes_ByteMetrics = new List<GCSampleType>();
            var sampleTypes_CountMetrics = new List<GCSampleType>();
            var sampleTypes_GaugeMetrics = new List<GCSampleType>();
            var sampleTypes_PercentMetrics = new List<GCSampleType>();

            //This dictionary defines which GCSampleTypes SHOULD be found in which metric type list.
            var expectationsDict = new Dictionary<GCSampleType, List<GCSampleType>>()
            {
                { GCSampleType.Gen0Size, sampleTypes_ByteMetrics },
                { GCSampleType.Gen0Promoted, sampleTypes_ByteMetrics },
                { GCSampleType.Gen1Size, sampleTypes_ByteMetrics },
                { GCSampleType.Gen1Promoted, sampleTypes_ByteMetrics },
                { GCSampleType.Gen2Size, sampleTypes_ByteMetrics },
                { GCSampleType.Gen2Survived, sampleTypes_ByteMetrics },
                { GCSampleType.LOHSize, sampleTypes_ByteMetrics },
                { GCSampleType.LOHSurvived, sampleTypes_ByteMetrics },
                { GCSampleType.HandlesCount, sampleTypes_GaugeMetrics },
                { GCSampleType.InducedCount, sampleTypes_CountMetrics },
                { GCSampleType.PercentTimeInGc, sampleTypes_PercentMetrics },
                { GCSampleType.Gen0CollectionCount, sampleTypes_CountMetrics },
                { GCSampleType.Gen1CollectionCount, sampleTypes_CountMetrics },
                { GCSampleType.Gen2CollectionCount, sampleTypes_CountMetrics },
            };

            //Arrange to capture which sample types ACTUALLY generated which metric types/shapes
            Mock.Arrange(() => metricBuilder.TryBuildGCBytesMetric(Arg.IsAny<GCSampleType>(), Arg.IsAny<long>()))
                .DoInstead<GCSampleType, long>((type, _) => { sampleTypes_ByteMetrics.Add(type); });

            Mock.Arrange(() => metricBuilder.TryBuildGCCountMetric(Arg.IsAny<GCSampleType>(), Arg.IsAny<int>()))
                .DoInstead<GCSampleType, int>((type, _) => { sampleTypes_CountMetrics.Add(type); });

            Mock.Arrange(() => metricBuilder.TryBuildGCGaugeMetric(Arg.IsAny<GCSampleType>(), Arg.IsAny<float>()))
                .DoInstead<GCSampleType, float>((type, _) => { sampleTypes_GaugeMetrics.Add(type); });

            Mock.Arrange(() => metricBuilder.TryBuildGCPercentMetric(Arg.IsAny<GCSampleType>(), Arg.IsAny<float>()))
                .DoInstead<GCSampleType, float>((type, _) => { sampleTypes_PercentMetrics.Add(type); });

            var transformer = new GcSampleTransformer(metricBuilder, _metricAggregator);

            //Act
            transformer.Transform(_sampleData);

            //Assert
            //Ensure that all of the sample types have a corresponding expected metric shape
            //A failure here indicates that new GCSampleTypes have been added, but the type of metric that it generates has not been identified.
            Assert.That(expectationsDict, Has.Count.EqualTo(_sampleTypesCount), "Not all GCSampleTypes have a metric shape associated with them.  expectationsDic is missing entries");

            //Validate that each SampleType generated the expected MetricType
            foreach (var q in expectationsDict)
            {
                Assert.That(q.Value, Does.Contain(q.Key), $"GC Sample Type {q.Key} was not of the expected metric type.");
            }
        }

        /// <summary>
        /// Ensures that the transformer clamps negative values for count samples.
        /// </summary>
        [Test]
        public void TransformerClampsNegativeCountValuesToZero()
        {
            var countTypes = new List<GCSampleType>()
            {
                GCSampleType.InducedCount,
                GCSampleType.Gen0CollectionCount,
                GCSampleType.Gen1CollectionCount,
                GCSampleType.Gen2CollectionCount
            };

            foreach(var countMetric in countTypes)
            {
                _sampleData[countMetric] = -1;
            }

            //Collect metrics that are generated from sample data
            var generatedMetrics = new Dictionary<string, MetricWireModel>();
            Mock.Arrange(() => _metricAggregator.Collect(Arg.IsAny<MetricWireModel>()))
                .DoInstead<MetricWireModel>((metric) => { generatedMetrics.Add(metric.MetricNameModel.Name, metric); });

            //Act
            _transformer.Transform(_sampleData);

            Assert.That(generatedMetrics.Any(x => x.Value.DataModel.Value0 < 0), Is.False);
        }
    }
}
