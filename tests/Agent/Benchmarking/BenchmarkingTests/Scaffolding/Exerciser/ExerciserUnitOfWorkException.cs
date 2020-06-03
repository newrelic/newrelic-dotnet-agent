using System;

namespace BenchmarkingTests.Scaffolding.CodeExerciser
{
    public class ExerciserUnitOfWorkException : Exception
    {
        public int UnitOfWorkIdLocal { get; private set; }
        public int UnitOfWorkIdGlobal { get; private set; }
        public int ThreadId { get; private set; }

        public ExerciserUnitOfWorkException(int threadId, int uowIdLocal, int uowIdGlobal, Exception ex, string phase)
            : base($"Encountered exception performing a unit of work during '{phase}'.", ex)
        {
            ThreadId = threadId;
            UnitOfWorkIdGlobal = uowIdGlobal;
            UnitOfWorkIdLocal = uowIdLocal;
        }
    }
}
