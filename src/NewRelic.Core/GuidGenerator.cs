// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace NewRelic.Core
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
            try
            {
                return _traceGeneratorFunc();
            }
            catch (Exception ex)
            {
                // If the app does not reference System.Diagnostics.DiagnosticSource then Activity.Current will not be available.
                // A FileNotFoundException occurs when System.Diagnostics.DiagnosticSource is unavailble.

                if (!(ex is FileNotFoundException))
                {
                    Log.Warn(ex, "Unexpected exception type when attempting to generate a trace ID from Activity.Current");
                }

                // Fall back to using our standard method of generating traceIds.
                Log.Info($"Trace IDs will be generated using the standard generator");
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
            if (Activity.Current != default && Activity.Current.IdFormat == ActivityIdFormat.W3C)
            {
                return Activity.Current.TraceId.ToString();
            }

            return GenerateTraceId();
        }
    }
}
