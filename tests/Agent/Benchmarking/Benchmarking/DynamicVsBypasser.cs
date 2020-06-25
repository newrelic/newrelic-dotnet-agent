/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using NBench;
using NewRelic.Reflection;

namespace Benchmarking
{
    // This set of tests was used to decide between dynamic or VisibilityBypasser in RabbitMQ BasicPublishWrappers.
    // The results showed that dynamic was faster.  This was unexpected, since we expected VisibilityBypasserto be slightly faster
    // Given the results we decided to use dynamic.  This includes easy of use and its readable nature.
    // We did want to preserve the code and the test link in a place that made sense and alongside the code.
    // https://docs.google.com/spreadsheets/d/1QKQdK0PE0r_gScXzLGB48_lJ5G9tUzpblLDm9WTygYg/edit?usp=sharing

    public class DynamicVsBypasser
    {
        private const string ReflectionCounterName = "ReflectionCounter";

        private Counter _counter;
        private List<object> _methodArguments;

        private Func<object, IDictionary<string, object>> _getMethodInfo;
        public Func<object, IDictionary<string, object>> GetMethodInfo => _getMethodInfo ?? (_getMethodInfo = VisibilityBypasser.Instance.GeneratePropertyAccessor<IDictionary<string, object>>("Benchmarking", "Benchmarking.RadicalContainer", "Headers"));

        [PerfSetup]
        public void Setup(BenchmarkContext context)
        {
            _methodArguments = new List<object>();
            var radicalContainer = new RadicalContainer();
            radicalContainer.PopulateHeaders();
            _methodArguments.Add(radicalContainer);

            _counter = context.GetCounter(ReflectionCounterName);
        }

        [PerfBenchmark(
            Description = "Bypasser",
            RunMode = RunMode.Throughput,
            NumberOfIterations = 3,
            TestMode = TestMode.Test,
            SkipWarmups = false)]
        [CounterThroughputAssertion(ReflectionCounterName, MustBe.GreaterThanOrEqualTo, 12000000)]
        [MemoryAssertion(MemoryMetric.TotalBytesAllocated, MustBe.LessThanOrEqualTo, 2108000)]
        public void ReflectOnWhatYouHaveDone()
        {
            var stopWatch = Stopwatch.StartNew();

            while (stopWatch.ElapsedMilliseconds < 20000)
            {
                var radicalContainer = (object)_methodArguments[0];
                var headers = GetMethodInfo.Invoke(radicalContainer);
                _counter.Increment();
            }

            stopWatch.Stop();
        }

        [PerfBenchmark(
            Description = "Dynamic",
            RunMode = RunMode.Throughput,
            NumberOfIterations = 3,
            TestMode = TestMode.Test,
            SkipWarmups = false)]
        [CounterThroughputAssertion(ReflectionCounterName, MustBe.GreaterThanOrEqualTo, 11000000)]
        [MemoryAssertion(MemoryMetric.TotalBytesAllocated, MustBe.LessThanOrEqualTo, 2108000)]
        public void YouHaveADynamicPersonality()
        {
            var stopWatch = Stopwatch.StartNew();

            while (stopWatch.ElapsedMilliseconds < 20000)
            {
                var radicalContainer = (dynamic)_methodArguments[0];
                var headers = (Dictionary<string, object>)radicalContainer.Headers;
                _counter.Increment();
            }

            stopWatch.Stop();
        }
    }

    public class RadicalContainer
    {
        public Dictionary<string, object> Headers => new Dictionary<string, object>();

        public Dictionary<string, string> Stuff => new Dictionary<string, string>();
        public Dictionary<int, string> Things => new Dictionary<int, string>();

        public string Name => string.Empty;
        public string Id => string.Empty;
        public string ParentName => string.Empty;
        public float Rating => 0.0f;
        public bool? AlwaysLate => false;

        public void PopulateHeaders()
        {
            Headers.Add("newrelic", "payload");
            Headers.Add("latitude", 51.5252949);
            Headers.Add("abool", false);
            Headers.Add("alist", new List<string> { "apha", "beta", "gama" });
        }
    }
}
