// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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
