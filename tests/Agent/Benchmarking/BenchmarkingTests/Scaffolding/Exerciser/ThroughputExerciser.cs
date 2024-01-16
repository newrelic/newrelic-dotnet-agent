// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace BenchmarkingTests.Scaffolding.CodeExerciser
{
    /// <summary>
    /// Performs as many units of work within a time window using x threads.
    /// For example, 10 threads each should create as many guids as possible within a 1 minutes period.
    /// </summary>
    public class ThroughputExerciser : Exerciser<ThroughputExerciser>
    {
        public ThroughputExerciser ForDuration(TimeSpan duration)
        {
            _duration = duration;
            return this;
        }

        public ThroughputExerciser ForDuration(uint milliseconds)
        {
            return ForDuration(new TimeSpan(0, 0, 0, 0, (int)milliseconds));
        }

        private TimeSpan _duration = new TimeSpan(0, 0, 1);

        protected override void ExerciserThreadImpl(int threadId)
        {
            var endTime = DateTime.Now.Add(_duration);

            var unitOfWorkIdLocal = 0;
            while (DateTime.Now <= endTime)
            {
                ExecIteration_Worker_PerformUnitOfWork(threadId, unitOfWorkIdLocal);
                unitOfWorkIdLocal++;
            }
        }
    }
}
