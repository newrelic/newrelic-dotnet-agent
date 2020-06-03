namespace BenchmarkingTests.Scaffolding.CodeExerciser
{
    /// <summary>
    /// The iterative exerciser will perform the exercised function x times (default 1) in a series of threads (default 1)
    /// For example: 10 threads each generating 1000 Guids
    /// For example: Run The SQLParser once
    /// </summary>
    public class IterativeExerciser : Exerciser<IterativeExerciser>
    {
        public IterativeExerciser PerformUnitOfWorkNTimes(uint countTimes)
        {
            _countIterations = countTimes;
            return this;
        }

        private uint _countIterations = 1;

        protected override void ExerciserThreadImpl(int threadId)
        {
            for (var i = 0; i < _countIterations; i++)
            {
                ExecIteration_Worker_PerformUnitOfWork(threadId, i);
            }
        }
    }
}
