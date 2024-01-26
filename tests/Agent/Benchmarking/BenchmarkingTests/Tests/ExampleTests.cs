// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using BenchmarkingTests.Scaffolding.Benchmarker;
using BenchmarkingTests.Scaffolding.CodeExerciser;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NUnit.Framework;
using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Collections.Generic;
using System.Data;
using NewRelic.Core;
using NewRelic.Parsing;
using System.Threading;

namespace BenchmarkingTests.Tests
{
    [TestFixture(Ignore = "yes")]
    public class ExampleTests
    {
        [SetUp]
        public void SetUp()
        {
        }

        [TearDown]
        public static void TearDown()
        {
        }

        [Test]
        public void Example_ExerciserAlone()
        {
            //Exerciser by itself
            var iterativeExerciser = IterativeExerciser.Create()
                .DoThisUnitOfWork(() => { var newGuid = GuidGenerator.GenerateNewRelicGuid(); })
                .PerformUnitOfWorkNTimes(1000)
                .UsingThreads(10);

            var iterativeExerciserResults = iterativeExerciser.ExecAll();

            //10 threads each running 1K units of work can produce 10K results
            Assert.That(iterativeExerciserResults.CountUnitsOfWorkPerformed, Is.EqualTo(10000));
        }

        [Test]
        public void Example_BenchmarkerWrappingExerciser()
        {
            //Benchmarker wrapping exerciser
            var benchmarkResult = NewRelicGuidGeneratorBenchmark.Execute();

            Assert.Multiple(() =>
            {
                Assert.That(benchmarkResult.CountUnitsOfWorkExecuted_Mean, Is.EqualTo(NewRelicGuidGeneratorBenchmark.CountUowTotal));
                Assert.That(benchmarkResult.Duration_Mean_Nanoseconds, Is.LessThanOrEqualTo(1000));
            });
        }


        public class NewRelicGuidGeneratorBenchmark : BenchmarkDotNetWrapper<NewRelicGuidGeneratorBenchmark>
        {
            public const int CountUowPerThread = 1000;
            public static uint CountThreads = BenchmarkingHelpers.CountAvailableCores + 1;
            public static int CountUowTotal = CountUowPerThread * (int)CountThreads;

            public override Exerciser Exerciser => IterativeExerciser.Create()
                .DoThisUnitOfWork(() => { var newGuid = GuidGenerator.GenerateNewRelicGuid(); })
                .PerformUnitOfWorkNTimes(CountUowPerThread)
                .UsingThreads(CountThreads);
        }

        public class StandardGuidBenchmark : BenchmarkDotNetWrapper<StandardGuidBenchmark>
        {
            public const int CountUowPerThread = 1000;
            public static uint CountThreads = BenchmarkingHelpers.CountAvailableCores + 1;
            public static int CountUowTotal = CountUowPerThread * (int)CountThreads;

            public override Exerciser Exerciser => IterativeExerciser.Create()
                .DoThisUnitOfWork(() => { var newGuid = Guid.NewGuid(); })
                .PerformUnitOfWorkNTimes(CountUowPerThread)
                .UsingThreads(CountThreads);
        }

        [Test]
        public void Example_CompareTwoBenchmarks()
        {
            var guidResult = NewRelicGuidGeneratorBenchmark.Execute();
            var referenceResult = StandardGuidBenchmark.Execute();

            var comparer = BenchmarkResultComparer.Create(guidResult, referenceResult);

            // the time it takes to generate 1000 guids on 10 threads should never run longer than 4x
            // the time it takes to generate 1000 strings on 10-threads should never run mmor
            Assert.That(comparer.Duration_Mean_Nanoseconds_Ratio, Is.LessThan(4));
        }


        [Test]
        public void TestDoSomethingBad()
        {
            var badIdea = new Dictionary<int, Guid>();

            Guid DoSomethingWrong(int key, Guid val)
            {
                if (!badIdea.ContainsKey(key))
                {
                    Thread.Sleep(100);      //sleep for 100ms
                    badIdea.Add(key, val);
                    return val;
                }

                return badIdea[key];
            }


            //This causes a contention problem
            var exerciser = IterativeExerciser.Create()
                .UsingThreads(10)
                .PerformUnitOfWorkNTimes(50)
                .DoThisUnitOfWork((threadId, unitOfWorkIdLocal, unitOfWorkIdGlobal) =>
                {
                    DoSomethingWrong(unitOfWorkIdLocal, Guid.NewGuid());
                });

            Assert.Throws<ExerciserException>(() => exerciser.ExecAll());
        }

        [Test]
        public void Example_DatabaseStatementParser()
        {
            DatabaseStatementParser dbParser = null;

            var cachedThroughput = ThroughputExerciser.Create()
                .UsingThreads(10)
                .ForDuration(1000)  // 1 second
                .DoThisToSetup(() => { dbParser = new DatabaseStatementParser(); })
                .DoThisUnitOfWork((threadId, uowIdLocal, uowIdGlobal) =>
                {
                    var stmt = dbParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, $"SELECT * FROM dbo.User WHERE UserID = {uowIdLocal}");
                })
                .ExecAll();

            var notCachedThroughput = ThroughputExerciser.Create()
                .UsingThreads(10)
                .ForDuration(1000)  // 1 second
                .DoThisToSetup(() => { dbParser = new DatabaseStatementParser(); })
                .DoThisUnitOfWork((threadId, uowIdLocal, uowIdGlobal) =>
                {
                    var stmt = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, $"SELECT * FROM dbo.User WHERE UserID = {uowIdLocal}");
                })
                .ExecAll();

            //Caching makes things better
            Assert.That(cachedThroughput.CountUnitsOfWorkPerformed, Is.GreaterThan(notCachedThroughput.CountUnitsOfWorkPerformed));

            //Chaching makes things 50% better
            //Assert.Greater(cachedThroughput.CountUnitsOfWorkPerformed, notCachedThroughput.CountUnitsOfWorkPerformed * 1.5);
        }

    }
}
