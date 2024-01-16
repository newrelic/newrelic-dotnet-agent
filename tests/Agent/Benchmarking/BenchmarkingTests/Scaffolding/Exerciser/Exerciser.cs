// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace BenchmarkingTests.Scaffolding.CodeExerciser
{
    /// <summary>
    /// An exerciser allows calling of specific code functions in a pattern
    /// Likely to be used by a Benchmarker to get performance results for testing.
    /// </summary>
    public abstract class Exerciser
    {
        protected Action _fxExerciserSetup;
        protected Action _fxExerciserTeardown;

        protected virtual void IterationSetup() { }
        protected virtual void Iteration_TearDown() { }

        /// <summary>
        /// Describes a unit of work function.
        /// </summary>
        /// <param name="threadId">The indexId of the thread</param>
        /// <param name="unitOfWorkIDLocal">The id of the unit of work local to the thread.</param>
        /// <param name="unitOfWorkIDGlobal">The id of the unit of work across all threads</param>
        public delegate void UnitOfWorkDelegate(int threadId, int unitOfWorkIDLocal, int unitOfWorkIDGlobal);

        protected UnitOfWorkDelegate _fxUnitOfWorkBefore;
        protected UnitOfWorkDelegate _fxUnitOfWork;
        protected UnitOfWorkDelegate _fxUnitOfWorkAfter;

        private Thread[] _threads;

        protected uint _countThreads = 1;

        //Holds information about the execution of the current iteration
        private ExerciserResult _currentIterationResult;

        /// <summary>
        /// This is what each thread is doing during the test.
        /// </summary>
        protected abstract void ExerciserThreadImpl(int threadId);

        /// <summary>
        /// Executes all steps of the exerciser with one call
        /// </summary>
        /// <returns></returns>
        public ExerciserResult ExecAll()
        {
            ExecSetup();

            ExecIterationBefore();

            ExecIteration();

            ExecIterationAfter();

            ExecTeardown();

            return _currentIterationResult;
        }

        /// <summary>
        /// Exoposes the overall setup function to be called individually.
        /// Useful for benchmarking harness
        /// </summary>
        public void ExecSetup()
        {
            _fxExerciserSetup?.Invoke();
        }

        /// <summary>
        /// Exoposes the overall teardown function to be called individually.
        /// Useful for benchmarking harness
        /// </summary>
        public void ExecTeardown()
        {
            _fxExerciserTeardown?.Invoke();
        }

        /// <summary>
        /// Exposes the functions to be executed before the beginning of the run
        /// These methods are not measured by benchmarking tools
        /// </summary>
        public void ExecIterationBefore()
        {
            _currentIterationResult = new ExerciserResult();

            //Perform any interation setup steps required by the exerciser
            IterationSetup();

            //If we have more than one thread, create (but don't start) the required threads
            if (_countThreads > 1)
            {
                _threads = new Thread[_countThreads];

                //Create the threads outside of the execution so as to not have this setup be part of the measurement
                for (var i = 0; i < _countThreads; i++)
                {
                    var threadId = i;
                    var threadStart = new ThreadStart(() => { ExecIterationWorker(threadId); });
                    _threads[i] = new Thread(threadStart);
                }
            }
        }

        /// <summary>
        /// Exposes the function to execute an iteration of the test
        /// </summary>
        /// <returns></returns>
        public ExerciserResult ExecIteration()
        {
            _currentIterationResult.Start();

            //If we only have one thread, perform the work on the same thread.
            if (_countThreads == 1)
            {
                ExecIterationWorker(0);
            }
            else
            {
                for (var i = 0; i < _countThreads; i++)
                {
                    _threads[i].Start();
                }

                for (var i = 0; i < _countThreads; i++)
                {
                    _threads[i].Join();
                }
            }

            _currentIterationResult.End();

            if (_currentIterationResult.Exceptions.Any())
            {
                throw new ExerciserException($"Encountered {_currentIterationResult.Exceptions.Count} exceptions while performing the workload.", _currentIterationResult.Exceptions.ToArray());
            }

            return _currentIterationResult;
        }

        /// <summary>
        /// Exposes the functions to be executed after the run of an iteration of
        /// the exerciser.
        /// </summary>
        public void ExecIterationAfter()
        {
            Iteration_TearDown();
        }

        /// <summary>
        /// This function is what a single thread of the exerciser will be doing
        /// It will catch exceptions and record them.
        /// </summary>
        private void ExecIterationWorker(int threadId)
        {
            try
            {
                ExerciserThreadImpl(threadId);
            }
            catch (Exception ex)
            {
                _currentIterationResult.RecordException(ex);
            }
        }

        /// <summary>
        /// Combines the unit of work functions into a single package to be called by the worker.
        /// Makes it easier for the implementors of Exerciser
        /// </summary>
        protected void ExecIteration_Worker_PerformUnitOfWork(int threadId, int unitOfWorkIdLocal)
        {
            var unitOfWorkIdGlobal = _currentIterationResult.IncrementUnitsofWorkPerformed();
            var currentPhase = "unknown";

            try
            {
                currentPhase = "Before";
                _fxUnitOfWorkBefore?.Invoke(threadId, unitOfWorkIdLocal, unitOfWorkIdGlobal);

                currentPhase = "The Unit of Work";
                _fxUnitOfWork(threadId, unitOfWorkIdLocal, unitOfWorkIdGlobal);

                currentPhase = "After";
                _fxUnitOfWorkAfter?.Invoke(threadId, unitOfWorkIdLocal, unitOfWorkIdGlobal);
            }
            catch (Exception ex)
            {
                throw new ExerciserUnitOfWorkException(threadId, unitOfWorkIdLocal, unitOfWorkIdGlobal, ex, currentPhase);
            }
        }
    }

    public abstract class Exerciser<T> : Exerciser where T : Exerciser, new()
    {
        public static T Create()
        {
            return new T();
        }

        /// <summary>
        /// The number of threads to be used when exercising
        /// </summary>
        /// <param name="countThreads"></param>
        /// <returns></returns>
        public T UsingThreads(uint countThreads)
        {
            _countThreads = countThreads;
            return this as T;
        }

        /// <summary>
        /// Any functions that help setup the iteration of the test
        /// </summary>
        /// <param name="fx"></param>
        /// <returns></returns>
        public T DoThisToSetup(Action fx)
        {
            _fxExerciserSetup = fx;
            return this as T;
        }

        /// <summary>
        /// Any functions that happen before the unit of work is performed
        /// </summary>
        /// <param name="fx"></param>
        /// <returns></returns>
        public T DoThisBeforeEachUnitOfWork(UnitOfWorkDelegate fx)
        {
            _fxUnitOfWorkBefore = fx;
            return this as T;
        }

        /// <summary>
        /// Any functions that happen before the unit of work is performed
        /// </summary>
        /// <param name="fx"></param>
        /// <returns></returns>
        public T DoThisBeforeEachUnitOfWork(Action fx)
        {
            _fxUnitOfWorkBefore = (threadId, unitOfWorkIdLocal, unitOfWorkIdGlobal) =>
            {
                fx();
            };

            return this as T;
        }

        /// <summary>
        /// This is the unit of work being exercised
        /// </summary>
        /// <param name="fx"></param>
        /// <returns></returns>
        public T DoThisUnitOfWork(UnitOfWorkDelegate fx)
        {
            _fxUnitOfWork = fx;
            return this as T;
        }

        /// <summary>
        /// This is the unit of work being exercised
        /// </summary>
        /// <param name="fx"></param>
        /// <returns></returns>
        public T DoThisUnitOfWork(Action fx)
        {
            _fxUnitOfWork = (threadId, unitOfWorkIdLocal, unitOfWorkIdGlobal) =>
            {
                fx();
            };
            return this as T;
        }



        /// <summary>
        /// Any functions that happen after the unit of work is performed
        /// </summary>
        /// <param name="fx"></param>
        /// <returns></returns>
        public T DoThisAfterEachUnitOfWork(UnitOfWorkDelegate fx)
        {
            _fxUnitOfWorkAfter = fx;
            return this as T;
        }

        /// <summary>
        /// This is the unit of work being exercised
        /// </summary>
        /// <param name="fx"></param>
        /// <returns></returns>
        public T DoThisAfterEachUnitOfWork(Action fx)
        {
            _fxUnitOfWorkAfter = (threadId, unitOfWorkIdLocal, unitOfWorkIdGlobal) =>
            {
                fx();
            };
            return this as T;
        }

        /// <summary>
        /// Any functions that tear down the exerciser (like object disposal)
        /// </summary>
        /// <param name="fx"></param>
        /// <returns></returns>
        public T DoThisToTearDown(Action fx)
        {
            _fxExerciserTeardown = fx;
            return this as T;
        }
    }
}
