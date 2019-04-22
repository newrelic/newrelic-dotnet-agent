using BenchmarkDotNet.Analysers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BenchmarkingTests.Scaffolding.Benchmarker
{
	/// <summary>
	/// Custom Diagnoser that will interpret captured log text and parse out
	/// the Count of Units of Work Performed.
	/// </summary>
	public class UnitsOfWorkDiagnoser : IDiagnoser
	{
		private const string SearchTextExceptions = BenchmarkDotNetWrapper.OutputTagExerciserResult + " - " + BenchmarkDotNetWrapper.MetricNameCountExceptions + ": ";
		private static readonly int SearchTextExceptionsLength = SearchTextExceptions.Length;

		private const string SearchTextCountUnitsOfWork = BenchmarkDotNetWrapper.OutputTagExerciserResult + " - " + BenchmarkDotNetWrapper.MetricNameCountUnitsOfWorkPerformed + ": ";
		private static readonly int SearchTextCountUnitsOfWorkLength = SearchTextCountUnitsOfWork.Length;

		private UnitsOfWorkLogInterceptor _uowLogInterceptor;

		public UnitsOfWorkDiagnoser(UnitsOfWorkLogInterceptor uowLogInterceptor)
		{
			_uowLogInterceptor = uowLogInterceptor;
		}

		private const string DiagnoserID = nameof(UnitsOfWorkDiagnoser);
		public IEnumerable<string> Ids => new[] { DiagnoserID };

		public IEnumerable<IExporter> Exporters => Array.Empty<IExporter>();

		public IEnumerable<IAnalyser> Analysers => Array.Empty<IAnalyser>();

		public void DisplayResults(ILogger logger) { }

		public BenchmarkDotNet.Diagnosers.RunMode GetRunMode(BenchmarkCase benchmarkCase) => BenchmarkDotNet.Diagnosers.RunMode.NoOverhead;

		public void Handle(HostSignal signal, DiagnoserActionParameters parameters)
		{
			//Only turn on the recording of information at certain points.
			//For example, ignore the warm up, etc.
			switch (signal)
			{
				case HostSignal.BeforeActualRun:
					_uowLogInterceptor.StartReading();
					break;

				case HostSignal.AfterActualRun:
					_uowLogInterceptor.StopReading();
					break;

				default:
					break;
			}
		}

		public IEnumerable<Metric> ProcessResults(DiagnoserResults results)
		{
			var unitsOfWork = _uowLogInterceptor.CapturedInfo
				.Where(x => x.StartsWith(SearchTextCountUnitsOfWork))
				.Select(x => int.Parse(x.Substring(SearchTextCountUnitsOfWorkLength)))
				.ToList();

			if (unitsOfWork.Any())
			{
				var avg = unitsOfWork.Average();
				var sum = unitsOfWork.Sum(d => Math.Pow(d - avg, 2));
				var stdDev = Math.Sqrt((sum) / (unitsOfWork.Count() - 1));


				yield return new Metric(CountUnitsOfWorkPerformedMetricDescriptor.Instance_Min, unitsOfWork.Min());
				yield return new Metric(CountUnitsOfWorkPerformedMetricDescriptor.Instance_Avg, avg);
				yield return new Metric(CountUnitsOfWorkPerformedMetricDescriptor.Instance_Max, unitsOfWork.Max());
				yield return new Metric(CountUnitsOfWorkPerformedMetricDescriptor.Instance_StdDev, stdDev);


			}
			else
			{
				yield return new Metric(CountUnitsOfWorkPerformedMetricDescriptor.Instance_Min, 0);
				yield return new Metric(CountUnitsOfWorkPerformedMetricDescriptor.Instance_Avg, 0);
				yield return new Metric(CountUnitsOfWorkPerformedMetricDescriptor.Instance_Max, 0);
				yield return new Metric(CountUnitsOfWorkPerformedMetricDescriptor.Instance_StdDev, 0);

			}

			var exceptions = _uowLogInterceptor.CapturedInfo
				.Where(x => x.StartsWith(SearchTextExceptions))
				.Select(x => int.Parse(x.Substring(SearchTextExceptionsLength)))
				.ToList();

			yield return new Metric(CountExceptionsMetricDescriptor.Instance, exceptions.Any() ? exceptions.Sum() : 0);
		}

		public IEnumerable<ValidationError> Validate(ValidationParameters validationParameters) => Array.Empty<ValidationError>();

		private class CountUnitsOfWorkPerformedMetricDescriptor : IMetricDescriptor
		{
			private readonly string _metricName;

			public CountUnitsOfWorkPerformedMetricDescriptor(string metricName)
			{
				_metricName = metricName;
			}

			internal static readonly IMetricDescriptor Instance_Min = new CountUnitsOfWorkPerformedMetricDescriptor(BenchmarkDotNetWrapper.MetricNameCountUnitsOfWorkPerformedMin);
			internal static readonly IMetricDescriptor Instance_Max = new CountUnitsOfWorkPerformedMetricDescriptor(BenchmarkDotNetWrapper.MetricNameCountUnitsOfWorkPerformedMax);
			internal static readonly IMetricDescriptor Instance_Avg = new CountUnitsOfWorkPerformedMetricDescriptor(BenchmarkDotNetWrapper.MetricNameCountUnitsOfWorkPerformedAvg);
			internal static readonly IMetricDescriptor Instance_StdDev = new CountUnitsOfWorkPerformedMetricDescriptor(BenchmarkDotNetWrapper.MetricNameCountUnitsOfWorkPerformedStdDev);

			public string Id => _metricName;
			public string DisplayName => _metricName;
			public string Legend => $"Number of times the workload function was exercised";
			public string NumberFormat => "N0";
			public UnitType UnitType => UnitType.Dimensionless;
			public string Unit => string.Empty;
			public bool TheGreaterTheBetter => true;
		}

		private class CountExceptionsMetricDescriptor : IMetricDescriptor
		{
			internal static readonly IMetricDescriptor Instance = new CountExceptionsMetricDescriptor();

			public string Id => BenchmarkDotNetWrapper.MetricNameCountExceptions;
			public string DisplayName => BenchmarkDotNetWrapper.MetricNameCountExceptions;
			public string Legend => $"Number of exceptions encountered";
			public string NumberFormat => "N0";
			public UnitType UnitType => UnitType.Dimensionless;
			public string Unit => string.Empty;
			public bool TheGreaterTheBetter => true;
		}
	}
}