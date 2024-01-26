// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using BenchmarkingTests.Scaffolding.CodeExerciser;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace BenchmarkingTests.ScaffoldingTests
{
    [TestFixture]
    public class ExerciserTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void ThrowsExceptionIfAnyUnitsOfWorkFail()
        {
            var exerciser = IterativeExerciser.Create()
                .UsingThreads(5)
                .PerformUnitOfWorkNTimes(1000)
                .DoThisUnitOfWork((threadId, uowIdLocal, uowIdGlobal) =>
                {
                    if (threadId == 3 && uowIdGlobal > 50)
                    {
                        throw new Exception();
                    }
                });

            Assert.Throws<ExerciserException>(() => { exerciser.ExecAll(); });
        }

        [Test]
        public void ContainsDetailedInnerExceptionForUnitOfWorkFailures()
        {
            var exerciser = IterativeExerciser.Create()
                .UsingThreads(5)
                .PerformUnitOfWorkNTimes(1000)
                .DoThisUnitOfWork((threadId, uowIdLocal, uowIdGlobal) =>
                {
                    if (threadId == 3 && uowIdGlobal > 50)
                    {
                        throw new Exception();
                    }
                });

            ExerciserException caughtException = null;

            try
            {
                exerciser.ExecAll();
            }
            catch (ExerciserException ex)
            {
                caughtException = ex;
            }

            Assert.That(caughtException, Is.Not.Null, "Expected an exception to be thrown and it was not");
            Assert.Multiple(() =>
            {
                Assert.That(caughtException.EncounteredExceptions.Count(), Is.EqualTo(1));
                Assert.That(caughtException.EncounteredExceptions.First(), Is.InstanceOf<ExerciserUnitOfWorkException>(), "Exception is not of correct type");
            });

            ExerciserUnitOfWorkException exerciserUnitOfWorkException = caughtException.EncounteredExceptions.First() as ExerciserUnitOfWorkException;
            Assert.Multiple(() =>
            {
                Assert.That(exerciserUnitOfWorkException!.UnitOfWorkIdGlobal, Is.GreaterThan(50));
                Assert.That(exerciserUnitOfWorkException.ThreadId, Is.EqualTo(3));
            });
        }

        [Test]
        public void CountUnitsOfWorkAreReportedAccurately()
        {
            const int expectedUnitsOfWork = 1000;
            const int countThreads = 5;

            var iterativeExerciserUnitsOfWorkExecuted = 0;
            var iterativeExerciserResult = IterativeExerciser.Create()
                .UsingThreads(countThreads)
                .PerformUnitOfWorkNTimes(expectedUnitsOfWork)
                .DoThisUnitOfWork(() => { Interlocked.Increment(ref iterativeExerciserUnitsOfWorkExecuted); })
                .ExecAll();

            var workQueueExerciserUnitsOfWorkExecuted = 0;
            var workQueueExerciserResult = WorkQueueExerciser.Create()
                .UsingThreads(countThreads)
                .WithWorkQueueSize(expectedUnitsOfWork)
                .DoThisUnitOfWork(() => { Interlocked.Increment(ref workQueueExerciserUnitsOfWorkExecuted); })
                .ExecAll();

            var throughputExerciserUnitsOfWorkExercised = 0;
            var throughputExerciserResult = ThroughputExerciser.Create()
                .UsingThreads(countThreads)
                .ForDuration(100)
                .DoThisUnitOfWork(() => { Interlocked.Increment(ref throughputExerciserUnitsOfWorkExercised); })
                .ExecAll();

            NrAssert.Multiple(
                () => Assert.That(iterativeExerciserResult.CountUnitsOfWorkPerformed, Is.EqualTo(expectedUnitsOfWork * countThreads)),
                () => Assert.That(iterativeExerciserUnitsOfWorkExecuted, Is.EqualTo(expectedUnitsOfWork * countThreads)),
                () => Assert.That(workQueueExerciserResult.CountUnitsOfWorkPerformed, Is.EqualTo(expectedUnitsOfWork)),
                () => Assert.That(workQueueExerciserUnitsOfWorkExecuted, Is.EqualTo(expectedUnitsOfWork)),
                () => Assert.That(throughputExerciserResult.CountUnitsOfWorkPerformed, Is.EqualTo(throughputExerciserUnitsOfWorkExercised))
            );
        }

        [Test]
        public void ExerciserMethodsAreCalledCorrectNumberOfTimes()
        {
            const int expectedUnitsOfWork = 100;
            const int countThreads = 5;

            ConcurrentBag<DateTime> setupTimes = null;
            ConcurrentBag<DateTime> beforeUOWTimes = null;
            ConcurrentBag<DateTime> uowTimes = null;
            ConcurrentBag<DateTime> afterUowTimes = null;
            ConcurrentBag<DateTime> teardownTimes = null;

            var iterativeExerciser = IterativeExerciser.Create()
                .UsingThreads(countThreads)
                .PerformUnitOfWorkNTimes(expectedUnitsOfWork)
                .DoThisToSetup(() => setupTimes.Add(DateTime.Now))
                .DoThisBeforeEachUnitOfWork(() => beforeUOWTimes.Add(DateTime.Now))
                .DoThisUnitOfWork(() => uowTimes.Add(DateTime.Now))
                .DoThisAfterEachUnitOfWork(() => afterUowTimes.Add(DateTime.Now))
                .DoThisToTearDown(() => teardownTimes.Add(DateTime.Now));

            var workQueueExerciser = WorkQueueExerciser.Create()
                .UsingThreads(countThreads)
                .WithWorkQueueSize(expectedUnitsOfWork)
                .DoThisToSetup(() => setupTimes.Add(DateTime.Now))
                .DoThisBeforeEachUnitOfWork(() => beforeUOWTimes.Add(DateTime.Now))
                .DoThisUnitOfWork(() => uowTimes.Add(DateTime.Now))
                .DoThisAfterEachUnitOfWork(() => afterUowTimes.Add(DateTime.Now))
                .DoThisToTearDown(() => teardownTimes.Add(DateTime.Now));

            var throughputExerciser = ThroughputExerciser.Create()
                .UsingThreads(countThreads)
                .ForDuration(100)
                .DoThisToSetup(() => setupTimes.Add(DateTime.Now))
                .DoThisBeforeEachUnitOfWork(() => beforeUOWTimes.Add(DateTime.Now))
                .DoThisUnitOfWork(() => uowTimes.Add(DateTime.Now))
                .DoThisAfterEachUnitOfWork(() => afterUowTimes.Add(DateTime.Now))
                .DoThisToTearDown(() => teardownTimes.Add(DateTime.Now));


            foreach (var exerciser in new Exerciser[] { iterativeExerciser, workQueueExerciser, throughputExerciser })
            {
                setupTimes = new ConcurrentBag<DateTime>();
                beforeUOWTimes = new ConcurrentBag<DateTime>();
                uowTimes = new ConcurrentBag<DateTime>();
                afterUowTimes = new ConcurrentBag<DateTime>();
                teardownTimes = new ConcurrentBag<DateTime>();

                var exerciserResult = exerciser.ExecAll();

                NrAssert.Multiple(
                () => Assert.That(setupTimes, Has.Count.EqualTo(1)),
                () => Assert.That(beforeUOWTimes, Has.Count.EqualTo(exerciserResult.CountUnitsOfWorkPerformed), $"{exerciser.GetType().FullName}, beforeUOW"),
                () => Assert.That(uowTimes, Has.Count.EqualTo(exerciserResult.CountUnitsOfWorkPerformed), $"{exerciser.GetType().FullName}, the Actual UOW"),
                () => Assert.That(afterUowTimes, Has.Count.EqualTo(exerciserResult.CountUnitsOfWorkPerformed), $"{exerciser.GetType().FullName}, After UOW"),
                () => Assert.That(teardownTimes, Has.Count.EqualTo(1)));
            }
        }

        [Test]
        public void ExerciserMethodsAreCalledInCorrectOrder()
        {
            const int expectedUnitsOfWork = 10;
            const int countThreads = 5;

            ConcurrentBag<DateTime> setupTimes = null;
            ConcurrentBag<DateTime> beforeUOWTimes = null;
            ConcurrentBag<DateTime> uowTimes = null;
            ConcurrentBag<DateTime> afterUowTimes = null;
            ConcurrentBag<DateTime> teardownTimes = null;

            var iterativeExerciser = IterativeExerciser.Create()
                .UsingThreads(countThreads)
                .PerformUnitOfWorkNTimes(expectedUnitsOfWork)
                .DoThisToSetup(() =>
                {
                    setupTimes.Add(DateTime.Now);
                    Thread.Sleep(50);
                })
                .DoThisBeforeEachUnitOfWork(() =>
                {
                    beforeUOWTimes.Add(DateTime.Now);
                    Thread.Sleep(10);
                })
                .DoThisUnitOfWork(() => uowTimes.Add(DateTime.Now))
                .DoThisAfterEachUnitOfWork(() =>
                {
                    Thread.Sleep(10);
                    afterUowTimes.Add(DateTime.Now);
                })
                .DoThisToTearDown(() =>
                {
                    Thread.Sleep(50);
                    teardownTimes.Add(DateTime.Now);
                });

            var workQueueExerciser = WorkQueueExerciser.Create()
                .UsingThreads(countThreads)
                .WithWorkQueueSize(expectedUnitsOfWork)
                .DoThisToSetup(() =>
                {
                    setupTimes.Add(DateTime.Now);
                    Thread.Sleep(50);
                })
                .DoThisBeforeEachUnitOfWork(() =>
                {
                    beforeUOWTimes.Add(DateTime.Now);
                    Thread.Sleep(10);
                })
                .DoThisUnitOfWork(() => uowTimes.Add(DateTime.Now))
                .DoThisAfterEachUnitOfWork(() =>
                {
                    Thread.Sleep(10);
                    afterUowTimes.Add(DateTime.Now);
                })
                .DoThisToTearDown(() =>
                {
                    Thread.Sleep(50);
                    teardownTimes.Add(DateTime.Now);
                });

            var throughputExerciser = ThroughputExerciser.Create()
                .UsingThreads(countThreads)
                .ForDuration(100)
                .DoThisToSetup(() =>
                {
                    setupTimes.Add(DateTime.Now);
                    Thread.Sleep(50);
                })
                .DoThisBeforeEachUnitOfWork(() =>
                {
                    beforeUOWTimes.Add(DateTime.Now);
                    Thread.Sleep(10);
                })
                .DoThisUnitOfWork(() => uowTimes.Add(DateTime.Now))
                .DoThisAfterEachUnitOfWork(() =>
                {
                    Thread.Sleep(10);
                    afterUowTimes.Add(DateTime.Now);
                })
                .DoThisToTearDown(() =>
                {
                    Thread.Sleep(50);
                    teardownTimes.Add(DateTime.Now);
                });

            foreach (var exerciser in new Exerciser[] { iterativeExerciser, workQueueExerciser, throughputExerciser })
            {
                setupTimes = new ConcurrentBag<DateTime>();
                beforeUOWTimes = new ConcurrentBag<DateTime>();
                uowTimes = new ConcurrentBag<DateTime>();
                afterUowTimes = new ConcurrentBag<DateTime>();
                teardownTimes = new ConcurrentBag<DateTime>();
                var exerciserResult = exerciser.ExecAll();

                NrAssert.Multiple(
                () => Assert.That(beforeUOWTimes.Min(), Is.GreaterThan(setupTimes.Max()), $"{exerciser.GetType().FullName} BeforeUOW detected before end of Setup"),
                () => Assert.That(uowTimes.Min(), Is.GreaterThan(beforeUOWTimes.Min()), $"{exerciser.GetType().FullName} UOW detected before end of BeforeUOW"),
                () => Assert.That(afterUowTimes.Max(), Is.GreaterThan(uowTimes.Max()), $"{exerciser.GetType().FullName} AfterUOW detected before end of UOW"),
                () => Assert.That(teardownTimes.Min(), Is.GreaterThan(afterUowTimes.Max()), $"{exerciser.GetType().FullName} Terdown detected before end of AfterUOW"));
            }
        }

        [Test]
        public void UnitOfWorkVariablesAreBeingCapturedCorrectly()
        {
            const int expectedUnitsOfWork = 100;
            const int countThreads = 5;

            //Update counters based on the global or local counter
            //later we will check to make sure that they are all set to 1
            ConcurrentDictionary<int, ConcurrentDictionary<int, int[]>> localCounters = null;
            ConcurrentDictionary<int, int[]> globalResultCounters = null;

            var iterativeExerciser = IterativeExerciser.Create()
                .UsingThreads(countThreads)
                .PerformUnitOfWorkNTimes(expectedUnitsOfWork)
                .DoThisBeforeEachUnitOfWork((threadId, uowIdLocal, uowIdGlobal) =>
                {
                    Interlocked.Increment(ref localCounters[threadId].GetOrAdd(uowIdLocal, new int[3])[0]);
                    Interlocked.Increment(ref globalResultCounters.GetOrAdd(uowIdGlobal, new int[3])[0]);

                })
                .DoThisUnitOfWork((threadId, uowIdLocal, uowIdGlobal) =>
                {
                    Interlocked.Increment(ref localCounters[threadId].GetOrAdd(uowIdLocal, new int[3])[1]);
                    Interlocked.Increment(ref globalResultCounters.GetOrAdd(uowIdGlobal, new int[3])[1]);
                })
                .DoThisAfterEachUnitOfWork((threadId, uowIdLocal, uowIdGlobal) =>
                {
                    Interlocked.Increment(ref localCounters[threadId].GetOrAdd(uowIdLocal, new int[3])[2]);
                    Interlocked.Increment(ref globalResultCounters.GetOrAdd(uowIdGlobal, new int[3])[2]);
                });

            var workQueueExerciser = WorkQueueExerciser.Create()
                .UsingThreads(countThreads)
                .WithWorkQueueSize(expectedUnitsOfWork)
                .DoThisBeforeEachUnitOfWork((threadId, uowIdLocal, uowIdGlobal) =>
                {
                    Interlocked.Increment(ref localCounters[threadId].GetOrAdd(uowIdLocal, new int[3])[0]);
                    Interlocked.Increment(ref globalResultCounters.GetOrAdd(uowIdGlobal, new int[3])[0]);

                })
                .DoThisUnitOfWork((threadId, uowIdLocal, uowIdGlobal) =>
                {
                    Interlocked.Increment(ref localCounters[threadId].GetOrAdd(uowIdLocal, new int[3])[1]);
                    Interlocked.Increment(ref globalResultCounters.GetOrAdd(uowIdGlobal, new int[3])[1]);
                })
                .DoThisAfterEachUnitOfWork((threadId, uowIdLocal, uowIdGlobal) =>
                {
                    Interlocked.Increment(ref localCounters[threadId].GetOrAdd(uowIdLocal, new int[3])[2]);
                    Interlocked.Increment(ref globalResultCounters.GetOrAdd(uowIdGlobal, new int[3])[2]);
                });

            var throughputExerciser = ThroughputExerciser.Create()
                .UsingThreads(countThreads)
                .ForDuration(100)
                .DoThisBeforeEachUnitOfWork((threadId, uowIdLocal, uowIdGlobal) =>
                {
                    Interlocked.Increment(ref localCounters[threadId].GetOrAdd(uowIdLocal, new int[3])[0]);
                    Interlocked.Increment(ref globalResultCounters.GetOrAdd(uowIdGlobal, new int[3])[0]);

                })
                .DoThisUnitOfWork((threadId, uowIdLocal, uowIdGlobal) =>
                {
                    Interlocked.Increment(ref localCounters[threadId].GetOrAdd(uowIdLocal, new int[3])[1]);
                    Interlocked.Increment(ref globalResultCounters.GetOrAdd(uowIdGlobal, new int[3])[1]);
                })
                .DoThisAfterEachUnitOfWork((threadId, uowIdLocal, uowIdGlobal) =>
                {
                    Interlocked.Increment(ref localCounters[threadId].GetOrAdd(uowIdLocal, new int[3])[2]);
                    Interlocked.Increment(ref globalResultCounters.GetOrAdd(uowIdGlobal, new int[3])[2]);
                });


            foreach (var exerciser in new Exerciser[] { iterativeExerciser, workQueueExerciser, throughputExerciser })
            {
                localCounters = new ConcurrentDictionary<int, ConcurrentDictionary<int, int[]>>();
                globalResultCounters = new ConcurrentDictionary<int, int[]>();

                foreach (var thread in localCounters)
                {
                    foreach (var uow in thread.Value)
                    {
                        NrAssert.Multiple(
                        () => Assert.That(uow.Value[0], Is.EqualTo(1), $"{exerciser.GetType().FullName}, threadID: {thread.Key}, uowIdLocal: {uow.Key}, BeforeUOW"),
                        () => Assert.That(uow.Value[1], Is.EqualTo(1), $"{exerciser.GetType().FullName}, threadID: {thread.Key}, uowIdLocal: {uow.Key}, TheUOW"),
                        () => Assert.That(uow.Value[2], Is.EqualTo(1), $"{exerciser.GetType().FullName}, threadID: {thread.Key}, uowIdLocal: {uow.Key}, AfterUOW"));
                    }
                }

                foreach (var uow in globalResultCounters)
                {
                    NrAssert.Multiple(
                        () => Assert.That(uow.Value[0], Is.EqualTo(1), $"{exerciser.GetType().FullName}, uowIdGlobal {uow.Key}, BeforeUOW"),
                        () => Assert.That(uow.Value[1], Is.EqualTo(1), $"{exerciser.GetType().FullName}, uowIdGlobal {uow.Key}, TheUOW"),
                        () => Assert.That(uow.Value[2], Is.EqualTo(1), $"{exerciser.GetType().FullName}, uowIdGlobal {uow.Key}, AfterUOW"));
                }

            }
        }

        [TearDown]
        public void Teardown()
        {

        }

    }
}
