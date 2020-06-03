namespace BenchmarkingTests.Scaffolding.Benchmarker
{
    public class BenchmarkResultComparer
    {
        public readonly BenchmarkResult ReferenceOperation;
        public readonly BenchmarkResult TestOperation;

        public static BenchmarkResultComparer Create(BenchmarkResult testedOperation, BenchmarkResult referenceOperation)
        {
            return new BenchmarkResultComparer(testedOperation, referenceOperation);
        }

        private BenchmarkResultComparer(BenchmarkResult testedOperation, BenchmarkResult referenceOperation)
        {
            TestOperation = testedOperation;
            ReferenceOperation = referenceOperation;
        }

        /// <summary>
        /// The difference between the two operations.  When positive, the test opeation utilized more than the reference operation
        /// </summary>
        public long Memory_BytesAllocated_Difference => TestOperation.Memory_BytesAllocated - ReferenceOperation.Memory_BytesAllocated;

        /// <summary>
        /// The difference between the two operations.  When positive, the test opeation utilized more than the reference operation
        /// </summary>
        public int GC_Gen0Collections_Difference => TestOperation.GC_Gen0Collections - ReferenceOperation.GC_Gen0Collections;

        /// <summary>
        /// The difference between the two operations.  When positive, the test opeation utilized more than the reference operation
        /// </summary>
        public int GC_Gen1Collections_Difference => TestOperation.GC_Gen1Collections - ReferenceOperation.GC_Gen1Collections;

        /// <summary>
        /// The difference between the two operations.  When positive, the test opeation utilized more than the reference operation
        /// </summary>
        public int GC_Gen2Collections_Difference => TestOperation.GC_Gen2Collections - ReferenceOperation.GC_Gen2Collections;

        /// <summary>
        /// The difference between the two operations.  When positive, the test opeation utilized more than the reference operation
        /// </summary>
        public double Duration_Mean_Nanoseconds_Difference => TestOperation.Duration_Mean_Nanoseconds - ReferenceOperation.Duration_Mean_Nanoseconds;

        /// <summary>
        /// The difference between the two operations.  When positive, the test opeation utilized more than the reference operation
        /// </summary>
        public double Duration_Min_Nanoseconds_Difference => TestOperation.Duration_Min_Nanoseconds - ReferenceOperation.Duration_Min_Nanoseconds;

        /// <summary>
        /// The difference between the two operations.  When positive, the test opeation utilized more than the reference operation
        /// </summary>
        public double Duration_Max_Nanoseconds_Difference => TestOperation.Duration_Max_Nanoseconds - ReferenceOperation.Duration_Max_Nanoseconds;

        /// <summary>
        /// The difference between the two operations.  When positive, the test opeation utilized more than the reference operation
        /// </summary>
        public double Duration_StdDev_Nanoseconds_Difference => TestOperation.Duration_StdDev_Nanoseconds - ReferenceOperation.Duration_StdDev_Nanoseconds;

        /// <summary>
        /// The difference between the two operations.  When positive, the test opeation utilized more than the reference operation
        /// </summary>
        public long CountUnitsOfWorkExecuted_Min_Difference => TestOperation.CountUnitsOfWorkExecuted_Min - ReferenceOperation.CountUnitsOfWorkExecuted_Min;

        /// <summary>
        /// The difference between the two operations.  When positive, the test opeation utilized more than the reference operation
        /// </summary>
        public long CountUnitsOfWorkExecuted_Max_Difference => TestOperation.CountUnitsOfWorkExecuted_Max - ReferenceOperation.CountUnitsOfWorkExecuted_Max;

        /// <summary>
        /// The difference between the two operations.  When positive, the test opeation utilized more than the reference operation
        /// </summary>
        public double CountUnitsOfWorkExecuted_Mean_Difference => TestOperation.CountUnitsOfWorkExecuted_Mean - ReferenceOperation.CountUnitsOfWorkExecuted_Mean;

        /// <summary>
        /// The difference between the two operations.  When positive, the test opeation utilized more than the reference operation
        /// </summary>
        public double CountUnitsOfWorkExecuted_StdDev_Difference => TestOperation.CountUnitsOfWorkExecuted_StdDev - ReferenceOperation.CountUnitsOfWorkExecuted_StdDev;


        /// <summary>
        /// The ratio between the two operations.  When > 1, the test opeation utilized more than the reference operation
        /// </summary>
        public double Memory_BytesAllocated_Ratio => TestOperation.Memory_BytesAllocated / (double)ReferenceOperation.Memory_BytesAllocated;

        /// <summary>
        /// The ratio between the two operations.  When > 1, the test opeation utilized more than the reference operation
        /// </summary>public double GC_Gen0Collections_Ratio => TestOperation.GC_Gen0Collections / (double)ReferenceOperation.GC_Gen0Collections;
        public double GC_Gen1Collections_Ratio => TestOperation.GC_Gen1Collections / (double)ReferenceOperation.GC_Gen1Collections;

        /// <summary>
        /// The ratio between the two operations.  When > 1, the test opeation utilized more than the reference operation
        /// </summary>public double GC_Gen0Collections_Ratio => TestOperation.GC_Gen0Collections / (double)ReferenceOperation.GC_Gen0Collections;
        public double GC_Gen2Collections_Ratio => TestOperation.GC_Gen2Collections / (double)ReferenceOperation.GC_Gen2Collections;

        /// <summary>
        /// The ratio between the two operations.  When > 1, the test opeation utilized more than the reference operation
        /// </summary>public double GC_Gen0Collections_Ratio => TestOperation.GC_Gen0Collections / (double)ReferenceOperation.GC_Gen0Collections;
        public double Duration_Mean_Nanoseconds_Ratio => TestOperation.Duration_Mean_Nanoseconds / (double)ReferenceOperation.Duration_Mean_Nanoseconds;

        /// <summary>
        /// The ratio between the two operations.  When > 1, the test opeation utilized more than the reference operation
        /// </summary>public double GC_Gen0Collections_Ratio => TestOperation.GC_Gen0Collections / (double)ReferenceOperation.GC_Gen0Collections;
        public double Duration_Min_Nanoseconds_Ratio => TestOperation.Duration_Min_Nanoseconds / (double)ReferenceOperation.Duration_Min_Nanoseconds;

        /// <summary>
        /// The ratio between the two operations.  When > 1, the test opeation utilized more than the reference operation
        /// </summary>public double GC_Gen0Collections_Ratio => TestOperation.GC_Gen0Collections / (double)ReferenceOperation.GC_Gen0Collections;
        public double Duration_Max_Nanoseconds_Ratio => TestOperation.Duration_Max_Nanoseconds / (double)ReferenceOperation.Duration_Max_Nanoseconds;

        /// <summary>
        /// The ratio between the two operations.  When > 1, the test opeation utilized more than the reference operation
        /// </summary>public double GC_Gen0Collections_Ratio => TestOperation.GC_Gen0Collections / (double)ReferenceOperation.GC_Gen0Collections;
        public double Duration_StdDev_Nanoseconds_Ratio => TestOperation.Duration_StdDev_Nanoseconds / (double)ReferenceOperation.Duration_StdDev_Nanoseconds;

        /// <summary>
        /// The ratio between the two operations.  When > 1, the test opeation utilized more than the reference operation
        /// </summary>public double GC_Gen0Collections_Ratio => TestOperation.GC_Gen0Collections / (double)ReferenceOperation.GC_Gen0Collections;
        public double CountUnitsOfWorkExecuted_Min_Ratio => TestOperation.CountUnitsOfWorkExecuted_Min / (double)ReferenceOperation.CountUnitsOfWorkExecuted_Min;

        /// <summary>
        /// The ratio between the two operations.  When > 1, the test opeation utilized more than the reference operation
        /// </summary>public double GC_Gen0Collections_Ratio => TestOperation.GC_Gen0Collections / (double)ReferenceOperation.GC_Gen0Collections;
        public double CountUnitsOfWorkExecuted_Max_Ratio => TestOperation.CountUnitsOfWorkExecuted_Max / (double)ReferenceOperation.CountUnitsOfWorkExecuted_Max;

        /// <summary>
        /// The ratio between the two operations.  When > 1, the test opeation utilized more than the reference operation
        /// </summary>public double GC_Gen0Collections_Ratio => TestOperation.GC_Gen0Collections / (double)ReferenceOperation.GC_Gen0Collections;
        public double CountUnitsOfWorkExecuted_Mean_Ratio => TestOperation.CountUnitsOfWorkExecuted_Mean / (double)ReferenceOperation.CountUnitsOfWorkExecuted_Mean;

        /// <summary>
        /// The ratio between the two operations.  When > 1, the test opeation utilized more than the reference operation
        /// </summary>public double GC_Gen0Collections_Ratio => TestOperation.GC_Gen0Collections / (double)ReferenceOperation.GC_Gen0Collections;
        public double CountUnitsOfWorkExecuted_StdDev_Ratio => TestOperation.CountUnitsOfWorkExecuted_StdDev / (double)ReferenceOperation.CountUnitsOfWorkExecuted_StdDev;
    }
}
