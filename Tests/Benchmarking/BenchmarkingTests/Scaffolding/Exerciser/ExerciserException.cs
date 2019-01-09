using System;

namespace BenchmarkingTests.Scaffolding.CodeExerciser
{
	public class ExerciserException : Exception
	{
		public Exception[] EncounteredExceptions;

		public ExerciserException(string message, params Exception[] innerExceptions) : base(message)
		{
			EncounteredExceptions = innerExceptions;
		}
	}
}
