using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using Telerik.JustMock;

// ReSharper disable InconsistentNaming
namespace NewRelic.Agent.Core.JsonConverters
{
	[TestFixture]
	public class ConvertMetricDataToJsonTests
	{
		private readonly IMetricNameService _metricNameService = Mock.Create<IMetricNameService>();
		private ConnectionHandler _connectionHandler;

		private object[] _wellformedMetricData;
		private string _wellformedJson = "[\"440491846668652\",1450462672.0,1450462710.0,[[{\"name\":\"DotNet/name\",\"scope\":\"WebTransaction/DotNet/name\"},[1,3.0,2.0,3.0,3.0,9.0]],[{\"name\":\"Custom/name\"},[1,4.0,3.0,4.0,4.0,16.0]]]]";

		private object[] _malformedMetricData_BadArraySize_Over4;
		private object[] _malformedMetricData_BadArraySize_Under4;

		private object[] _malformedMetricData_IncorrectTypeInArray_AgentId;
		private object[] _malformedMetricData_IncorrectTypeInArray_BeginTime_AsInt;
		private object[] _malformedMetricData_IncorrectTypeInArray_EndTime_AsInt;
		private object[] _malformedMetricData_IncorrectTypeInArray_MetricWireModels;

		private object[] _malformedMetricData_IEnumerableMetricWireModel_Empty;
		private object[] _malformedMetricData_IEnumerableMetricWireModel_Null;

		[SetUp]
		public void Setup()
		{
			Mock.Arrange(() => _metricNameService.RenameMetric(Arg.IsAny<string>())).Returns<string>(name => name);
			_connectionHandler = new ConnectionHandler(Mock.Create<JsonSerializer>(), Mock.Create<ICollectorWireFactory>(), Mock.Create<IProcessStatic>(), Mock.Create<IDnsStatic>(),
				Mock.Create<ILabelsService>(), Mock.Create<Environment>(), Mock.Create<ISystemInfo>(), Mock.Create<IAgentHealthReporter>());

			//valid/wellformed fixtures

			var validScopedMetric = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "WebTransaction/DotNet/name",
				MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3.0), TimeSpan.FromSeconds(2.0)));

			var validUnscopedMetric = MetricWireModel.BuildMetric(_metricNameService, "Custom/name", string.Empty,
				MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(4.0), TimeSpan.FromSeconds(3.0)));

			var validMetricWireModels = new List<MetricWireModel> { validScopedMetric, validUnscopedMetric };
			_wellformedMetricData = new object[4] { "440491846668652", 1450462672.0, 1450462710.0, validMetricWireModels };

			// invalid/malformed fixtures

			var emptyMetricWireModels = new List<MetricWireModel>();
			_malformedMetricData_IncorrectTypeInArray_AgentId = new object[4] { 0, 1450462672.0, 1450462710.0, validMetricWireModels };
			_malformedMetricData_IncorrectTypeInArray_BeginTime_AsInt = new object[4] { "440491846668652", 1450462672, 1450462710.0, validMetricWireModels };
			_malformedMetricData_IncorrectTypeInArray_EndTime_AsInt = new object[4] { "440491846668652", 1450462672.0, 1450462710, validMetricWireModels };
			_malformedMetricData_IncorrectTypeInArray_MetricWireModels = new object[4] { "440491846668652", 1450462672.0, 1450462710.0, "bad" };

			_malformedMetricData_BadArraySize_Over4 = new object[5] { "440491846668652", 1450462672.0, 1450462710.0, validMetricWireModels, "bad" };
			_malformedMetricData_BadArraySize_Under4 = new object[3] { "440491846668652", 1450462672.0, 1450462710.0 };
			_malformedMetricData_IEnumerableMetricWireModel_Empty = new object[4] { "440491846668652", 1450462672.0, 1450462710.0, emptyMetricWireModels };
			_malformedMetricData_IEnumerableMetricWireModel_Null = new object[4] { "440491846668652", 1450462672.0, 1450462710.0, null };
		}

		[Test]
		public void Serialize_NoErrors()
		{
			Assert.DoesNotThrow(() => _connectionHandler.ConvertMetricDataToJson(_wellformedMetricData));
		}

		[Test]
		public void Serialize_MatchesExpectedOutput()
		{
			var actualMetricData = _connectionHandler.ConvertMetricDataToJson(_wellformedMetricData);
			Assert.AreEqual(_wellformedJson, actualMetricData);
		}

		[Test]
		public void Serialize_BadArraySize_ArgumentOutOfRangeException()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => _connectionHandler.ConvertMetricDataToJson(_malformedMetricData_BadArraySize_Over4));
			Assert.Throws<ArgumentOutOfRangeException>(() => _connectionHandler.ConvertMetricDataToJson(_malformedMetricData_BadArraySize_Under4));
		}

		[Test]
		public void Serialize_IncorrectTypeInArray_ArgumentException()
		{
			Assert.Throws<ArgumentException>(() => _connectionHandler.ConvertMetricDataToJson(_malformedMetricData_IncorrectTypeInArray_AgentId));
			Assert.Throws<ArgumentException>(() => _connectionHandler.ConvertMetricDataToJson(_malformedMetricData_IncorrectTypeInArray_BeginTime_AsInt));
			Assert.Throws<ArgumentException>(() => _connectionHandler.ConvertMetricDataToJson(_malformedMetricData_IncorrectTypeInArray_EndTime_AsInt));
			Assert.Throws<ArgumentException>(() => _connectionHandler.ConvertMetricDataToJson(_malformedMetricData_IncorrectTypeInArray_MetricWireModels));
			Assert.Throws<ArgumentException>(() => _connectionHandler.ConvertMetricDataToJson(_malformedMetricData_IEnumerableMetricWireModel_Null));
		}
	}
}