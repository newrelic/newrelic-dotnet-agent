// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MultiFunctionApplicationHelpers.Libraries
{
    [Library]
    public static class ApiCalls
    {
        public static NewRelic.Api.Agent.IAgent Agent = NewRelic.Api.Agent.NewRelic.GetAgent();

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static ITraceMetadata TraceMetadata()
        {
            return Agent.TraceMetadata;
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static Dictionary<string, string> GetLinkingMetadata()
        {
            return Agent.GetLinkingMetadata();
        }

        [LibraryMethod]
        public static void TestTraceMetadata()
        {
            var traceMetadata = TraceMetadata();

            ConsoleMFLogger.Info($"TraceId: {traceMetadata.TraceId}, SpanId:{traceMetadata.SpanId}, IsSampled:{traceMetadata.IsSampled.ToString()}");
        }

        [LibraryMethod]
        public static void TestGetLinkingMetadata()
        {
            var getLinkingMetadata = GetLinkingMetadata();

            foreach (KeyValuePair<string, string> item in getLinkingMetadata)
            {
                ConsoleMFLogger.Info($"key: {item.Key}, value:{item.Value}");
            }
        }

        [LibraryMethod]
        public static void TestSetApplicationName(string applicationName)
        {
            NewRelic.Api.Agent.NewRelic.SetApplicationName(applicationName);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void TestSetTransactionUserId(string userId)
        {
            NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.SetUserId(userId);
        }

        [LibraryMethod]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void TestSetErrorGroupCallbackReturnsString(string callbackReturn)
        {
            NewRelic.Api.Agent.NewRelic.SetErrorGroupCallback((x) => callbackReturn);
        }

        [LibraryMethod]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void TestSetErrorGroupCallbackWithKeys()
        {
            // list of keys that are expected as part of agent spec
            var expectedKeys = new[]
            {
                "error.class",
                "error.message",
                "stack_trace",
                "transactionName",
                "request.uri",
                "error.expected"
                // The following attribute keys are expected to be available, but in this specific test, they are not.
                // "http.statusCode", (no http call in test)
                // "request.method" (no http call in test)
                // "transactionUiName" (Not available)
            };

            NewRelic.Api.Agent.NewRelic.SetErrorGroupCallback((dict) =>
            {
                if (dict == null)
                {
                    return "IReadOnlyDictionary was null";
                }

                if (dict.Count < 1)
                {
                    return "IReadOnlyDictionary Count was " + dict.Count;
                }

                var missingKeys = new List<string>();
                foreach (var key in expectedKeys)
                {
                    if (!dict.ContainsKey(key))
                    {
                        missingKeys.Add(key);
                    }
                }

                if (missingKeys.Count > 0)
                {
                    return "IReadOnlyDictionary missing keys: " + string.Join(", ", missingKeys);
                }

                return "success";
            });
        }

        [LibraryMethod]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void TestNullSetErrorGroupCallback()
        {
            NewRelic.Api.Agent.NewRelic.SetErrorGroupCallback((Func<IReadOnlyDictionary<string, object>, string>)null);
        }
    }
}
