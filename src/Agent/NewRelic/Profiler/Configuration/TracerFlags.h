#pragma once

namespace NewRelic { namespace Profiler { namespace Configuration
{
    // TracerFlags are set during the loading of instrumentation points from the
    // various instrumentation files (e.g., CoreInstrumentation).
    // This enumeration has to be kept in sync with the C# code in 
    // NewRelic.Agent.Core.Tracer\TracerFlags.cs and related C# code.
    //
    // These flags are or'ed together into an unsigned to make a set of flags.
    enum TracerFlags 
    {
        // Bits 31..24 are the transaction naming priority, aka "priority".
        // If set (eg, between 1 .. 7), try to name the current transaction using this tracer.
        // Multiple tracers may try to name the transaction.
        // The highest priority wins.

        // Bit 23 indicates if a method is considered async via usage of the AsyncStateMachineAttribute
        AsyncMethod = 1 << 23,
        OtherTransaction = 1 << 22,
        WebTransaction = 1 << 21,
        AttributeInstrumentation = 1 << 20,
        // Bit 19 is unused(?)

        // Bits 18..16 hold a 3-bit instrumentation level for this instrumenter.
        // Larger values imply more instrumentation.

        // Bits 15..8 are the individual boolean flags.
        UseInvocationTargetClassName = 1 << 15,

        CustomMetricName = 1 << 14,

        // (This comment from the extensions.xsd file.)
        // Internal Only.  Subject to future change.
        // By default, the agent will not create a new tracer if the parent tracer instruments the same class / method signature.
        SuppressRecursiveCalls = 1 << 13,

        GenerateScopedMetric = 1 << 12,

        GenerateUnscopedMetric = 1 << 11,

        // If set, then a transaction tracer segment is generated for this tracer.
        TransactionTracerSegment = 1 << 10,

        CombineMultipleInvocations = 1 << 9,

        FullClassMatch = 1 << 8
        // Bits 7..0 are unused(?).
    };
}}}
