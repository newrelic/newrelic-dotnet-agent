// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Logging;
using NewRelic.Reflection;
using System;
using System.Security.Cryptography;
using System.Threading;

namespace NewRelic.Agent.Core.Utilities
{
    [NrExcludeFromCodeCoverage]
    public static class GuidGenerator
    {
        /// Our testing shows that RngCryptoServiceProvider library is threadsafe and is more performant
        /// than Random library when generating random numbers during thread contention.
        private static readonly RNGCryptoServiceProvider RngCryptoServiceProvider = new RNGCryptoServiceProvider();
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
            var rndBytes = new byte[8];
            RngCryptoServiceProvider.GetBytes(rndBytes);
            return $"{BitConverter.ToUInt64(rndBytes, 0):x16}";
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
            var rndBytes = new byte[16];
            RngCryptoServiceProvider.GetBytes(rndBytes);


            return $"{BitConverter.ToUInt64(rndBytes, 0):x16}{BitConverter.ToUInt64(rndBytes, 8):x16}";
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
}
