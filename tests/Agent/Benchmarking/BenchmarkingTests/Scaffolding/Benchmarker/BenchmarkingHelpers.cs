using System;

namespace BenchmarkingTests.Scaffolding.Benchmarker
{
    public static class BenchmarkingHelpers
    {
        public static uint CountAvailableCores => (uint)Environment.ProcessorCount;
    }
}
