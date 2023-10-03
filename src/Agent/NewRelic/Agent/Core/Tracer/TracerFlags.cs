// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Tracer
{
    /// <summary>
    /// A bitset of tracer flags.
    /// These flags come to us from values scraped out of the xml instrumentation files, such as extensions/CoreInstrumentation.xml
    ///
    /// The bit assignments here MUST exactly match what is used in the C++ code InstrumentationFileParser.h
    /// </summary>
    [Flags]
    public enum TracerFlags : uint
    {
        Async = 1 << 23,
        OtherTransaction = 1 << 22,
        WebTransaction = 1 << 21,
        AttributeInstrumentation = 1 << 20,
        UseInvocationTargetClassName = 1 << 15,
        CustomMetricName = 1 << 14,
        SuppressRecursiveCalls = 1 << 13,
        GenerateScopedMetric = 1 << 12,
        GenerateUnscopedMetric = 1 << 11,
        TransactionTracerSegment = 1 << 10,
        CombineMultipleInvocations = 1 << 9,
        FullClassMatch = 1 << 8 // Indicates that this tracer is associated with a matcher that matches all methods in a class.
                                // Bits 7..0 are unused
    }

    /// <summary>
    /// Tracer arguments are stored in a 32 bit unsigned integer.  
    /// 
    /// The first byte is the transaction naming priority.
    /// The second is the instrumentation level.
    /// The third and fourth bytes are the TracerFlags.
    /// Keep this (fragile) code in sync with what goes on in InstrumentationFileParser.h and InstrumentationFileParser.cpp.
    /// </summary>
    public static class TracerArgument
    {

        // Returns the transaction naming priority, in the range 0..7.  The default is 0.
        // This value is sourced from an instrumentation file, such as extensions/CoreInstrumentation.xml
        // Treat 0 as null (because that's how null ends up being conveyed to us via the profiler)
        public static TransactionNamePriority? GetTransactionNamingPriority(uint tracerArguments)
        {
            var priority = (int)(tracerArguments >> 24) & 0x7;
            var result = (priority == 0) ? default(TransactionNamePriority?) : (TransactionNamePriority)priority;

            return result;
        }

        /// <summary>
        /// Checks to see if the "IsAsync" bit was set in the tracerArguments from the profiler.
        /// IsAsync will be true if the instrumented method uses AsyncStateMachineAttribute. This is typically
        /// added automatically when the `async` keyword is used on a method.
        /// </summary>
        /// <param name="tracerArguments"></param>
        /// <returns>true if async bit has been set on tracerArguments</returns>
        public static bool IsAsync(uint tracerArguments)
        {
            return IsFlagSet(tracerArguments, TracerFlags.Async);
        }

        public static bool IsFlagSet(uint tracerArguments, TracerFlags flag)
        {
            return (tracerArguments & (int)flag) != 0;
        }
    }
}
