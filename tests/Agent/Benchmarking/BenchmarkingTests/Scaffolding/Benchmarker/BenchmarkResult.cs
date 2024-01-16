// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace BenchmarkingTests.Scaffolding.Benchmarker
{
    public class BenchmarkResult
    {
        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public long Memory_BytesAllocated { get; set; }
        public int GC_Gen0Collections { get; set; }
        public int GC_Gen1Collections { get; set; }
        public int GC_Gen2Collections { get; set; }

        public double Duration_Mean_Nanoseconds { get; set; }
        public double Duration_Min_Nanoseconds { get; set; }
        public double Duration_Max_Nanoseconds { get; set; }
        public double Duration_StdDev_Nanoseconds { get; set; }


        public long CountUnitsOfWorkExecuted_Min { get; set; }
        public long CountUnitsOfWorkExecuted_Max { get; set; }
        public double CountUnitsOfWorkExecuted_Mean { get; set; }
        public double CountUnitsOfWorkExecuted_StdDev { get; set; }

        public double CountExceptions { get; set; }

        public BenchmarkResult()
        {
            StartTime = DateTime.UtcNow;
        }
    }

}
