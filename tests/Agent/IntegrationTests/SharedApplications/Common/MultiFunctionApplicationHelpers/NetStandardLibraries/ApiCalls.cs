// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
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
    }
}
