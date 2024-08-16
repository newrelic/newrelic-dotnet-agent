// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Logging;
using NewRelic.Reflection;
using System;
using System.Security.Cryptography;
using System.Threading;

namespace NewRelic.Agent.Core.Utilities
{
    public static class GuidGenerator
    {
        /// Our testing shows that RngCryptoServiceProvider library is threadsafe and is more performant
        /// than Random library when generating random numbers during thread contention.
        private static readonly RNGCryptoServiceProvider RngCryptoServiceProvider = new RNGCryptoServiceProvider();
        private static Func<string> _traceGeneratorFunc = GetTraceIdFromCurrentActivity;

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
            var retVal = _traceGeneratorFunc();
            if (retVal == null)
            {
                // Fall back to using our standard method of generating traceIds.
                Log.Info($"Trace IDs will be generated using the standard generator");
                Interlocked.Exchange(ref _traceGeneratorFunc, GenerateTraceId);
                return _traceGeneratorFunc();
            }

            return retVal;
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

            // get list of loaded assemblies
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            // find System.Diagnostics.DiagnosticSource
            var diagnosticSourceAssembly = Array.Find(assemblies, a => a.FullName.StartsWith("System.Diagnostics.DiagnosticSource"));
            if (diagnosticSourceAssembly == null) // customer app didn't reference the assembly
                return null;

            // find the Activity class
            var activityType = diagnosticSourceAssembly.GetType("System.Diagnostics.Activity");

            var fieldReadAccessor = VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>(activityType, "s_current");
            if (fieldReadAccessor == null)
                return null;

            var current = fieldReadAccessor(null);
            if (current == null)
                return null;

            // get the Value property
            var valuePropertyAccessor = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(current.GetType(), "Value");
            if (valuePropertyAccessor == null)
                return null;

            var value = valuePropertyAccessor(current);
            if (value == null)
                return null;

            // get IdFormat property
            var idFormatGetter = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(value.GetType(), "IdFormat");
            var idFormat = idFormatGetter(value);
            if (idFormat == null || Enum.GetName(idFormat.GetType(), idFormat) != "W3C") // make sure it's in W3C trace id format
                return null;

            // get TraceId property
            var traceIdGetter = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(value.GetType(), "TraceId");
            if (traceIdGetter == null)
                return null;
                
            return traceIdGetter(value).ToString();
        }
    }
}
