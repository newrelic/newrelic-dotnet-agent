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

			NrAssert.Multiple(
				() => Assert.IsNotNull(caughtException, "Expected an exception to be thrown and it was not"),
				() => Assert.AreEqual(1, caughtException.EncounteredExceptions.Count()),
				() => Assert.IsInstanceOf<ExerciserUnitOfWorkException>(caughtException.EncounteredExceptions.First(), "Exception is not of correct type"),
				() => Assert.Less(50, (caughtException.EncounteredExceptions.First() as ExerciserUnitOfWorkException).UnitOfWorkIdGlobal),
				() => Assert.AreEqual(3, (caughtException.EncounteredExceptions.First() as ExerciserUnitOfWorkException).ThreadId)
			);
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
				() => Assert.AreEqual(expectedUnitsOfWork * countThreads, iterativeExerciserResult.CountUnitsOfWorkPerformed),
				() => Assert.AreEqual(expectedUnitsOfWork * countThreads, iterativeExerciserUnitsOfWorkExecuted),
				() => Assert.AreEqual(expectedUnitsOfWork, workQueueExerciserResult.CountUnitsOfWorkPerformed),
				() => Assert.AreEqual(expectedUnitsOfWork, workQueueExerciserUnitsOfWorkExecuted),
				() => Assert.AreEqual(throughputExerciserUnitsOfWorkExercised, throughputExerciserResult.CountUnitsOfWorkPerformed)
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
				() => Assert.AreEqual(1, setupTimes.Count),
				() => Assert.AreEqual(exerciserResult.CountUnitsOfWorkPerformed, beforeUOWTimes.Count, $"{exerciser.GetType().FullName}, beforeUOW"),
				() => Assert.AreEqual(exerciserResult.CountUnitsOfWorkPerformed, uowTimes.Count, $"{exerciser.GetType().FullName}, the Actual UOW"),
				() => Assert.AreEqual(exerciserResult.CountUnitsOfWorkPerformed, afterUowTimes.Count, $"{exerciser.GetType().FullName}, After UOW"),
				() => Assert.AreEqual(1, teardownTimes.Count));
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
				() => Assert.Greater(beforeUOWTimes.Min(), setupTimes.Max(), $"{exerciser.GetType().FullName} BeforeUOW detected before end of Setup"),
				() => Assert.Greater(uowTimes.Min(), beforeUOWTimes.Min(), $"{exerciser.GetType().FullName} UOW detected before end of BeforeUOW"),
				() => Assert.Greater(afterUowTimes.Max(), uowTimes.Max(), $"{exerciser.GetType().FullName} AfterUOW detected before end of UOW"),
				() => Assert.Greater(teardownTimes.Min(), afterUowTimes.Max(), $"{exerciser.GetType().FullName} Terdown detected before end of AfterUOW"));
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
						() => Assert.AreEqual(1, uow.Value[0], $"{exerciser.GetType().FullName}, threadID: {thread.Key}, uowIdLocal: {uow.Key}, BeforeUOW"),
						() => Assert.AreEqual(1, uow.Value[1], $"{exerciser.GetType().FullName}, threadID: {thread.Key}, uowIdLocal: {uow.Key}, TheUOW"),
						() => Assert.AreEqual(1, uow.Value[2], $"{exerciser.GetType().FullName}, threadID: {thread.Key}, uowIdLocal: {uow.Key}, AfterUOW"));
					}
				}

				foreach (var uow in globalResultCounters)
				{
					NrAssert.Multiple(
						() => Assert.AreEqual(1, uow.Value[0], $"{exerciser.GetType().FullName}, uowIdGlobal {uow.Key}, BeforeUOW"),
						() => Assert.AreEqual(1, uow.Value[1], $"{exerciser.GetType().FullName}, uowIdGlobal {uow.Key}, TheUOW"),
						() => Assert.AreEqual(1, uow.Value[2], $"{exerciser.GetType().FullName}, uowIdGlobal {uow.Key}, AfterUOW"));
				}

			}
		}

		[TearDown]
		public void Teardown()
		{

		}

	}
}
