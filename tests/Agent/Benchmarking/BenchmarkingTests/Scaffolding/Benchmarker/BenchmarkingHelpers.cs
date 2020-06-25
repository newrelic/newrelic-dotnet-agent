/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;

namespace BenchmarkingTests.Scaffolding.Benchmarker
{
    public static class BenchmarkingHelpers
    {
        public static uint CountAvailableCores => (uint)Environment.ProcessorCount;
    }
}
