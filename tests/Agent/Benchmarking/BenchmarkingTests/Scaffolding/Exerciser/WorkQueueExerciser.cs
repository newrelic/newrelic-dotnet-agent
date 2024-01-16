// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace BenchmarkingTests.Scaffolding.CodeExerciser
{
    /// <summary>
    /// The Workqueue exerciser will spin up x threads that will, in-total, perform n units of work (exercized function)
    /// For example, 10 threads compete to generate 10K Guids
    /// </summary>
    public class WorkQueueExerciser : Exerciser<WorkQueueExerciser>
    {
        public WorkQueueExerciser WithWorkQueueSize(uint workQueueSize)
        {
            _workQueueSize = workQueueSize;
            return this;
        }

        private uint _workQueueSize = 1;

        private int _countWorkItemsRemaining;

        protected override void IterationSetup()
        {
            _countWorkItemsRemaining = (int)_workQueueSize;
        }

        protected override void ExerciserThreadImpl(int threadId)
        {
            var unitOfWorkIDLocal = 0;

            while (Interlocked.Decrement(ref _countWorkItemsRemaining) >= 0)
            {
                ExecIteration_Worker_PerformUnitOfWork(threadId, unitOfWorkIDLocal);
                unitOfWorkIDLocal++;
            }
        }
    }
}
