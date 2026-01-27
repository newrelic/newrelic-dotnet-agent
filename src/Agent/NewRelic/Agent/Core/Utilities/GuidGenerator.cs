// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Reflection;

namespace NewRelic.Agent.Core.Utilities;

[NrExcludeFromCodeCoverage]
public static class GuidGenerator
{
    private static Func<string> _traceGeneratorFunc = GetTraceIdFromCurrentActivity;

    private static bool _initialized;
    private static object _lockObj = new();
    private static bool _hasDiagnosticSourceReference;

    private static Func<object, object> _fieldReadAccessor;
    private static Func<object, object> _valuePropertyAccessor;
    private static Func<object, object> _traceIdGetter;
    private static Func<object, object> _idFormatGetter;

    /// <summary>
    /// Returns a newrelic style guid.
    /// https://source.datanerd.us/agents/agent-specs/blob/2ad6637ded7ec3784de40fbc88990e06525127b8/Cross-Application-Tracing-PORTED.md#guid
    /// </summary>
    /// <returns></returns>
    public static string GenerateNewRelicGuid()
    {
        Span<byte> b = stackalloc byte[8];
        var rng = NewRandomNumberGenerator.Current;

        Unsafe.WriteUnaligned(ref b[0], rng.Next());

        return FastToStringHelpers.FastToString(b);
    }

    public static string GenerateNewRelicTraceId()
    {
        try
        {
            var retVal = _traceGeneratorFunc();
            if (retVal == null)
            {
                if (!_hasDiagnosticSourceReference)
                {
                    // Fall back to using our standard method of generating traceIds if the application doesn't reference DiagnosticSource
                    Log.Info("No reference to DiagnosticSource; trace IDs will be generated using the standard generator");
                    Interlocked.Exchange(ref _traceGeneratorFunc, GenerateTraceId);
                    return _traceGeneratorFunc();
                }

                // couldn't get a traceId from the current activity (maybe there wasn't one), so fallback to the standard generator for this request only
                return GenerateTraceId();
            }

            return retVal;
        }
        catch (Exception e)
        {
            Log.Info(e, "Unexpected exception generating traceId using the current activity. Falling back to the standard generator");
            Interlocked.Exchange(ref _traceGeneratorFunc, GenerateTraceId);
            return _traceGeneratorFunc();
        }
    }

    private static string GenerateTraceId()
    {
        Span<byte> b = stackalloc byte[16];
        var rng = NewRandomNumberGenerator.Current;

        Unsafe.WriteUnaligned(ref b[0], rng.Next());
        Unsafe.WriteUnaligned(ref b[8], rng.Next());

        return FastToStringHelpers.FastToString(b);
    }

    private static string GetTraceIdFromCurrentActivity()
    {
        // because we ILRepack System.Diagnostics.DiagnosticSource, we have to look for the app's reference to it (if there is one)
        // and use reflection to get the trace id from the current activity

        // initialize one time
        if (!_initialized)
        {
            lock (_lockObj)
            {
                if (!_initialized)
                {
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                    // find System.Diagnostics.DiagnosticSource
                    var diagnosticSourceAssembly = Array.Find(assemblies, a => a.FullName.StartsWith("System.Diagnostics.DiagnosticSource"));
                    if (diagnosticSourceAssembly != null) // customer app might not reference the assembly
                    {
                        _hasDiagnosticSourceReference = true;

                        // find the Activity class
                        var activityType = diagnosticSourceAssembly.GetType("System.Diagnostics.Activity");
                        _fieldReadAccessor = VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>(activityType, "s_current");
                    }

                    _initialized = true;
                }
            }
        }

        if (!_hasDiagnosticSourceReference)
            return null;

        var current = _fieldReadAccessor(null); // s_current is a static, so we don't need an object instance
        if (current == null)
            return null;

        // get the Value property
        _valuePropertyAccessor ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(current.GetType(), "Value");
        var value = _valuePropertyAccessor(current);
        if (value == null)
            return null;

        // get IdFormat property
        _idFormatGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(value.GetType(), "IdFormat");
        var idFormat = _idFormatGetter(value);
        if (idFormat == null || Enum.GetName(idFormat.GetType(), idFormat) != "W3C") // make sure it's in W3C trace id format
            return null;

        // get TraceId property
        _traceIdGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(value.GetType(), "TraceId");
        return _traceIdGetter(value).ToString();
    }
}

/// <summary>
/// Implementation is the 64-bit random number generator based on the Xoshiro256StarStar algorithm (known as shift-register generators).
/// Taken from System.Diagnostics.DiagnosticSource implementation at https://github.com/dotnet/runtime/blob/main/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/RandomNumberGenerator.cs#L9
///
/// This implementation is not cryptographically secure, but is faster than using RNGCryptoServiceProvider.
/// It also aligns with the implementation used by System.Diagnostics.DiagnosticSource and OpenTelemetry.
/// </summary>
internal sealed class NewRandomNumberGenerator
{
    [ThreadStatic] private static NewRandomNumberGenerator t_random;

    private ulong _s0, _s1, _s2, _s3;

    public static NewRandomNumberGenerator Current => t_random ??= new NewRandomNumberGenerator();

    public unsafe NewRandomNumberGenerator()
    {
        do
        {
            Guid g1 = Guid.NewGuid();
            Guid g2 = Guid.NewGuid();
            ulong* g1p = (ulong*)&g1;
            ulong* g2p = (ulong*)&g2;
            _s0 = *g1p;
            _s1 = *(g1p + 1);
            _s2 = *g2p;
            _s3 = *(g2p + 1);

            // Guid uses the 4 most significant bits of the first long as the version which would be fixed and not randomized.
            // and uses 2 other bits in the second long for variants which would be fixed and not randomized too.
            // let's overwrite the fixed bits in each long part by the other long.
            _s0 = (_s0 & 0x0FFFFFFFFFFFFFFF) | (_s1 & 0xF000000000000000);
            _s2 = (_s2 & 0x0FFFFFFFFFFFFFFF) | (_s3 & 0xF000000000000000);
            _s1 = (_s1 & 0xFFFFFFFFFFFFFF3F) | (_s0 & 0x00000000000000C0);
            _s3 = (_s3 & 0xFFFFFFFFFFFFFF3F) | (_s2 & 0x00000000000000C0);
        }
        while ((_s0 | _s1 | _s2 | _s3) == 0);
    }

    private static ulong Rol64(ulong x, int k) => (x << k) | (x >> (64 - k));

    public long Next()
    {
        ulong result = Rol64(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;

        _s2 ^= t;
        _s3 = Rol64(_s3, 45);

        return (long)result;
    }
}

internal static class FastToStringHelpers
{
    /// <summary>
    /// Converts the specified <see cref="ReadOnlySpan{T}"/> of bytes to its hexadecimal string representation.
    /// </summary>
    /// <remarks>Each byte in the input span is converted to two hexadecimal characters in the
    /// resulting string. The conversion uses lowercase letters for hexadecimal digits (a-f).
    ///
    /// Taken from https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/System/HexConverter.cs#L219
    /// </remarks>
    /// <param name="bytes">The span of bytes to convert to a hexadecimal string.</param>
    /// <returns>A string containing the hexadecimal representation of the input bytes.</returns>
    public static string FastToString(ReadOnlySpan<byte> bytes)
    {
        Span<char> result = bytes.Length > 16 ?
            new char[bytes.Length * 2].AsSpan() :
            stackalloc char[bytes.Length * 2];

        int pos = 0;
        foreach (byte b in bytes)
        {
            ToCharsBuffer(b, result, pos, Casing.Lower);
            pos += 2;
        }
        return result.ToString();
    }

    // taken from https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/System/HexConverter.cs#L85
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ToCharsBuffer(byte value, Span<char> buffer, int startingIndex = 0, Casing casing = Casing.Upper)
    {
        uint difference = (((uint)value & 0xF0U) << 4) + ((uint)value & 0x0FU) - 0x8989U;
        uint packedResult = ((((uint)(-(int)difference) & 0x7070U) >> 4) + difference + 0xB9B9U) | (uint)casing;

        buffer[startingIndex + 1] = (char)(packedResult & 0xFF);
        buffer[startingIndex] = (char)(packedResult >> 8);
    }
    private enum Casing : uint
    {
        // Output [ '0' .. '9' ] and [ 'A' .. 'F' ].
        Upper = 0,

        // Output [ '0' .. '9' ] and [ 'a' .. 'f' ].
        // This works because values in the range [ 0x30 .. 0x39 ] ([ '0' .. '9' ])
        // already have the 0x20 bit set, so ORing them with 0x20 is a no-op,
        // while outputs in the range [ 0x41 .. 0x46 ] ([ 'A' .. 'F' ])
        // don't have the 0x20 bit set, so ORing them maps to
        // [ 0x61 .. 0x66 ] ([ 'a' .. 'f' ]), which is what we want.
        Lower = 0x2020U,
    }

}