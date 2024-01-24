// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;


namespace NewRelic.Reflection.UnitTests
{
    public class PerformanceComparison
    {
        public class Foo
        {
            public int _int32Field = 7;
        }

        [Test]
        public void PerformanceTest()
        {
            var fieldName = "_int32Field";
            var foo = new Foo();
            var iterations = 10000000;

            // warm up
            for (var i = 0; i < 1000; ++i)
                DoSomething(foo._int32Field);
            // time it
            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < iterations; ++i)
                DoSomething(foo._int32Field);
            Debug.WriteLine("Direct access:	" + stopwatch.Elapsed.TotalSeconds);


            // setup
            var fieldAccessor = VisibilityBypasser.Instance.GenerateFieldReadAccessor<Foo, int>(fieldName);
            // warm up
            for (var i = 0; i < 1000; ++i)
                DoSomething(fieldAccessor(foo));
            // time it
            stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < iterations; ++i)
                DoSomething(fieldAccessor(foo));
            Debug.WriteLine("Snazzy new stuff: " + stopwatch.Elapsed.TotalSeconds);


            // setup
            var fieldReflection = typeof(Foo).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            // warm up
            for (var i = 0; i < 1000; ++i)
                DoSomething(fieldReflection.GetValue(foo));
            // time it
            stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < iterations; ++i)
                DoSomething(fieldReflection.GetValue(foo));
            Debug.WriteLine("Reflection:	   " + stopwatch.Elapsed.TotalSeconds);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DoSomething(object thing)
        {

        }
    }
}
