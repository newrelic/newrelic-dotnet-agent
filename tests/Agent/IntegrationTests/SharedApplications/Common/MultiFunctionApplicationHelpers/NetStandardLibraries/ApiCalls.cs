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

        [LibraryMethod]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void TestSetErrorGroupCallback()
        {
            NewRelic.Api.Agent.NewRelic.SetErrorGroupCallback(ErrorGroupCallback);
        }

        private static string ErrorGroupCallback(IReadOnlyDictionary<string, object> attributes)
        {
            var errorGroupName = "OtherErrors";
            if (attributes.TryGetValue("error.message", out var errorMessage))
            {
                if (errorMessage.ToString() == "Test Message") // See AttributeInstrumentation.MakeWebTransactionWithException
                {
                    errorGroupName = "TestErrors";
                }
            }
            return errorGroupName;
        }

        /// <summary>
        /// Tests setting the current transaction name via the API
        /// </summary>
        /// <param name="category">Category</param>
        /// <param name="names">Comma-separated list of names to be applied, in order</param>
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void TestSetTransactionName(string category, string names)
        {
            var namesList = names.Split(',');
            foreach (var name in namesList)
            {
                NewRelic.Api.Agent.NewRelic.SetTransactionName(category, name);
            }
        }

        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void RecordDatastoreSegment(string vendor, string model, string operation,
             string commandText = null, string host = null, string portPathOrID = null, string databaseName = null)
        {
            var transaction = NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction;
            using (transaction.RecordDatastoreSegment(vendor, model, operation,
                commandText, host, portPathOrID, databaseName))
            {
                DatastoreWorker();
            }
        }

        private static void DatastoreWorker()
        {
            System.Threading.Thread.Sleep(1000);
        }

        [LibraryMethod]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void TestFullRecordDatastoreSegment()
        {
            RecordDatastoreSegment("MyVendor", "MyModel", "MyOperation",
                "MyCommandText", "MyHost", "MyPath", "MyDatabase");
        }

        [LibraryMethod]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void TestRequiredRecordDatastoreSegment()
        {
            RecordDatastoreSegment("MyVendor", "MyModel", "MyOperation");
        }
    }
}
