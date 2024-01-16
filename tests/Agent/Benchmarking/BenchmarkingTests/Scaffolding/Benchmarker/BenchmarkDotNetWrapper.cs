// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using BenchmarkingTests.Scaffolding.CodeExerciser;

namespace BenchmarkingTests.Scaffolding.Benchmarker
{
    public abstract class BenchmarkDotNetWrapper<T> : BenchmarkDotNetWrapper where T : BenchmarkDotNetWrapper, new()
    {
        public static BenchmarkResult Execute()
        {
            return Execute(true);
        }

        public static BenchmarkResult Execute(bool throwExceptionOnExerciserFailure)
        {
            var type = typeof(T);

            if ((type.IsNested && !type.IsNestedPublic) || (!type.IsNested && !type.IsPublic))
            {
                throw new BenchmarkerClassNotPublicException($"Benchmark class '{typeof(T).FullName}' is not public.  Benchmark.Net requires it to be public.");
            }


            var logInterceptor = new UnitsOfWorkLogInterceptor();

            var config = ManualConfig.CreateEmpty();

            config.Add(Job.Default
                .With(Platform.X64)
                .WithGcServer(true)
                .WithGcConcurrent(true)
                .WithEvaluateOverhead(true)
                .WithOutlierMode(BenchmarkDotNet.Mathematics.OutlierMode.All));

            config.Add(MemoryDiagnoser.Default);
            config.Add(new UnitsOfWorkDiagnoser(logInterceptor));
            config.Add(logInterceptor);
            config.Add(DefaultConfig.Instance);
            config.Add(JitOptimizationsValidator.DontFailOnError);

            var result = new BenchmarkResult();

            var benchmarkResult = BenchmarkRunner.Run<T>(config);


            if (benchmarkResult.Reports.Count() != 1)
            {
                throw new Exception("BenchmarkDotNet did not produce benchmark results.");
            }


            //Update benchmark results
            result.EndTime = DateTime.UtcNow;
            result.Duration_Mean_Nanoseconds = benchmarkResult.Reports[0].ResultStatistics.Mean;
            result.Duration_Min_Nanoseconds = benchmarkResult.Reports[0].ResultStatistics.Min;
            result.Duration_Max_Nanoseconds = benchmarkResult.Reports[0].ResultStatistics.Max;
            result.Duration_StdDev_Nanoseconds = benchmarkResult.Reports[0].ResultStatistics.StandardDeviation;
            result.Memory_BytesAllocated = benchmarkResult.Reports[0].GcStats.BytesAllocatedPerOperation;
            result.GC_Gen0Collections = benchmarkResult.Reports[0].GcStats.Gen0Collections;
            result.GC_Gen1Collections = benchmarkResult.Reports[0].GcStats.Gen1Collections;
            result.GC_Gen2Collections = benchmarkResult.Reports[0].GcStats.Gen2Collections;
            result.CountUnitsOfWorkExecuted_Min = Convert.ToInt64(benchmarkResult.Reports[0].Metrics[MetricNameCountUnitsOfWorkPerformedMin].Value);
            result.CountUnitsOfWorkExecuted_Mean = benchmarkResult.Reports[0].Metrics[MetricNameCountUnitsOfWorkPerformedAvg].Value;
            result.CountUnitsOfWorkExecuted_Max = Convert.ToInt64(benchmarkResult.Reports[0].Metrics[MetricNameCountUnitsOfWorkPerformedMax].Value);
            result.CountUnitsOfWorkExecuted_StdDev = Convert.ToInt64(benchmarkResult.Reports[0].Metrics[MetricNameCountUnitsOfWorkPerformedStdDev].Value);
            result.CountExceptions = Convert.ToInt64(benchmarkResult.Reports[0].Metrics[MetricNameCountExceptions].Value);

            if (throwExceptionOnExerciserFailure && result.CountExceptions > 0)
            {
                throw new Exception($"Exerciser Encountered {result.CountExceptions} during its runs");
            }

            return result;
        }
    }

    public abstract partial class BenchmarkDotNetWrapper
    {

        public const string OutputTagExerciserResult = "Exerciser Result";

        public const string MetricNameCountExceptions = "CountExceptions";

        public const string MetricNameCountUnitsOfWorkPerformed = "CountUnitsOfWorkPerformed";
        public const string MetricNameCountUnitsOfWorkPerformedMin = MetricNameCountUnitsOfWorkPerformed + "_Min";
        public const string MetricNameCountUnitsOfWorkPerformedMax = MetricNameCountUnitsOfWorkPerformed + "_Max";
        public const string MetricNameCountUnitsOfWorkPerformedAvg = MetricNameCountUnitsOfWorkPerformed + "_Avg";
        public const string MetricNameCountUnitsOfWorkPerformedStdDev = MetricNameCountUnitsOfWorkPerformed + "_StdDev";

        /// <summary>
        /// This is the actual exerciser pattern being benchmarked.
        /// </summary>
        public abstract Exerciser Exerciser { get; }
        private Exerciser _exerciserInst;

        public BenchmarkDotNetWrapper()
        {
            _exerciserInst = Exerciser;
        }

        [GlobalSetup]
        public void Setup()
        {
            _exerciserInst.ExecSetup();
        }

        [IterationSetup]
        public void IterationSetup()
        {
            _currentIterationException = null;
            try
            {
                _exerciserInst.ExecIterationBefore();
            }
            catch (Exception ex)
            {
                _currentIterationException = ex;
            }
        }

        private ExerciserResult _currentIterationResult;
        private Exception _currentIterationException;

        [Benchmark(Baseline = true)]
        public void Benchmark()
        {
            if (_currentIterationException == null)
            {
                try
                {
                    _currentIterationResult = _exerciserInst.ExecIteration();
                }
                catch (Exception ex)
                {
                    _currentIterationException = ex;
                }
            }
        }


        [IterationCleanup]
        public void IterationCleanup()
        {
            if (_currentIterationException == null)
            {
                try
                {
                    _exerciserInst.ExecIterationAfter();
                }
                catch (Exception ex)
                {
                    _currentIterationException = ex;
                }
            }

            Console.WriteLine($"{OutputTagExerciserResult} - {MetricNameCountUnitsOfWorkPerformed}: {(_currentIterationResult != null ? _currentIterationResult.CountUnitsOfWorkPerformed : 0)}");
            Console.WriteLine($"{OutputTagExerciserResult} - {MetricNameCountExceptions}: {(_currentIterationException != null ? 1 : 0)}");
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _exerciserInst.ExecTeardown();
        }
    }
}
