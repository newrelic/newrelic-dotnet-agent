// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Collections.Generic;

namespace BenchmarkingTests.Scaffolding.CodeExerciser
{
    public class ExerciserResult
    {
        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }

        private int _countUnitsOfWorkPerformed = 0;
        public int CountUnitsOfWorkPerformed => _countUnitsOfWorkPerformed;

        private ConcurrentBag<Exception> _exceptions = new ConcurrentBag<Exception>();
        public List<Exception> Exceptions => _exceptions.ToList();

        public void RecordException(Exception ex)
        {
            _exceptions.Add(ex);
        }

        public void Start()
        {
            StartTime = DateTime.UtcNow;
        }

        public void End()
        {
            EndTime = DateTime.UtcNow;
        }

        public int IncrementUnitsofWorkPerformed()
        {
            return Interlocked.Increment(ref _countUnitsOfWorkPerformed) - 1;
        }
    }
}
