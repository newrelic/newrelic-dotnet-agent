/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace BasicMvcApplication.Controllers
{
    public class CustomInstrumentationController : Controller
    {
        [HttpGet]
        public string Get()
        {
            // Call various methods which have been custom instrumented
            CustomMethodDefaultWrapper();
            CustomMethodDefaultTracer();
            CustomMethodUnknownWrapperName();
            CustomMethodNoWrapperName();
            CustomSegmentTransactionSegmentWrapper("CustomSegmentName");
            CustomSegmentAlternateParameterNamingTheSegment(5, "AlternateCustomSegmentName");
            CustomSegmentTracer("CustomSegmentNameFromTracer");

            return "Worked";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CustomMethodDefaultWrapper()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(5));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CustomMethodDefaultTracer()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(5));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CustomMethodUnknownWrapperName()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(5));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CustomMethodNoWrapperName()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(5));
        }

        [HttpGet]
        public string GetIgnoredByIgnoreTransactionWrapper()
        {
            // Call various methods which have been custom instrumented
            CustomMethodIgnoreTransactionWrapper();

            return "Worked";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CustomMethodIgnoreTransactionWrapper()
        {

        }

        [HttpGet]
        public string GetIgnoredByIgnoreTransactionTracerFactory()
        {
            // Call various methods which have been custom instrumented
            CustomMethodIgnoreTransactionTracerFactory();

            return "Worked";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CustomMethodIgnoreTransactionTracerFactory()
        {

        }

        [HttpGet]
        public async Task<string> GetIgnoredByIgnoreTransactionWrapperAsync()
        {
            // Call various methods which have been custom instrumented
            await CustomMethodIgnoreTransactionWrapperAsync();

            return "Worked";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task CustomMethodIgnoreTransactionWrapperAsync()
        {
            await Task.Delay(5);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string CustomSegmentTransactionSegmentWrapper(string segmentName)
        {
            return segmentName;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string CustomSegmentAlternateParameterNamingTheSegment(int x, string segmentName)
        {
            x++;
            return segmentName;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string CustomSegmentTracer(string segmentName)
        {
            return segmentName;
        }
    }
}
