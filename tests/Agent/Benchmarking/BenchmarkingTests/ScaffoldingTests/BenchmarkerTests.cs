// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using BenchmarkingTests.Scaffolding.Benchmarker;
using BenchmarkingTests.Scaffolding.CodeExerciser;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Threading;

namespace BenchmarkingTests.ScaffoldingTests
{
    [TestFixture(Ignore = "yes")]
    public class BenchmarkerTests
    {
        public class SimpleThroughputBenchmark : BenchmarkDotNetWrapper<SimpleThroughputBenchmark>
        {
            private int _counterValue = 0;
            public int CounterValue => _counterValue;

            public const int CountThreads = 3;
            public const int DurationMilliseconds = 200;

            public override Exerciser Exerciser => ThroughputExerciser.Create()
                .UsingThreads(CountThreads)
                .ForDuration(DurationMilliseconds)
                .DoThisUnitOfWork(() => Interlocked.Increment(ref _counterValue));
        }

        public class SimpleFailingBenchmark : BenchmarkDotNetWrapper<SimpleFailingBenchmark>
        {
            public const int CountThreads = 3;

            public override Exerciser Exerciser => IterativeExerciser.Create()
                .UsingThreads(CountThreads)
                .PerformUnitOfWorkNTimes(100)
                .DoThisUnitOfWork(() => throw new Exception("Forced Error"));
        }

        class NotPublicNestedBenchmark : BenchmarkDotNetWrapper<NotPublicNestedBenchmark>
        {
            public const int CountThreads = 3;

            public override Exerciser Exerciser => IterativeExerciser.Create()
                .UsingThreads(CountThreads)
                .PerformUnitOfWorkNTimes(100)
                .DoThisUnitOfWork(() => throw new Exception("Forced Error"));
        }

        [Test]
        public void BenchmarkRunsAndResultsAreReasonable()
        {
            var benchmarkResult = SimpleThroughputBenchmark.Execute();

            NrAssert.Multiple(
                () => Assert.AreEqual(0, benchmarkResult.CountExceptions),
                () => Assert.Greater(benchmarkResult.CountUnitsOfWorkExecuted_Min, 0),
                () => Assert.GreaterOrEqual(benchmarkResult.CountUnitsOfWorkExecuted_Mean, benchmarkResult.CountUnitsOfWorkExecuted_Min),
                () => Assert.GreaterOrEqual(benchmarkResult.CountUnitsOfWorkExecuted_Max, benchmarkResult.CountUnitsOfWorkExecuted_Mean),
                () => Assert.GreaterOrEqual(benchmarkResult.Duration_Min_Nanoseconds, SimpleThroughputBenchmark.DurationMilliseconds),
                () => Assert.GreaterOrEqual(benchmarkResult.Duration_Mean_Nanoseconds, benchmarkResult.Duration_Min_Nanoseconds),
                () => Assert.GreaterOrEqual(benchmarkResult.Duration_Max_Nanoseconds, benchmarkResult.Duration_Mean_Nanoseconds),
                () => Assert.Greater(benchmarkResult.EndTime, benchmarkResult.StartTime)
            );
        }

        [Test]
        public void ExerciserFailureBubblesUp_ReportsError()
        {
            var benchmarkResult = SimpleFailingBenchmark.Execute(false);
            Assert.Greater(benchmarkResult.CountExceptions, 0);
        }

        [Test]
        public void ExerciserFailureBubblesUp_ThrowsExceptionByDefault()
        {
            NrAssert.Multiple(
                () => Assert.Throws<Exception>(() => SimpleFailingBenchmark.Execute()),
                () => Assert.Throws<Exception>(() => SimpleFailingBenchmark.Execute(true)));
        }

        [Test]
        public void BenchmarkerFailsWhenClassNotPublic()
        {
            NrAssert.Multiple(
                () => Assert.Throws<BenchmarkerClassNotPublicException>(() => NotPublicNotNestedBenchmark.Execute()),
                () => Assert.Throws<BenchmarkerClassNotPublicException>(() => NotPublicNestedBenchmark.Execute()));
        }
    }

    class NotPublicNotNestedBenchmark : BenchmarkDotNetWrapper<NotPublicNotNestedBenchmark>
    {
        public const int CountThreads = 3;

        public override Exerciser Exerciser => IterativeExerciser.Create()
            .UsingThreads(CountThreads)
            .PerformUnitOfWorkNTimes(100)
            .DoThisUnitOfWork(() => throw new Exception("Forced Error"));
    }
}
