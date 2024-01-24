// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Metrics
{
    [TestFixture]
    public class MetricNamesTests
    {
        [Test]
        public void GetDotNetInvocation()
        {
            var metricName = MetricNames.GetDotNetInvocation("class", "method");
            var sameMetricName = MetricNames.GetDotNetInvocation("class", "method");
            Assert.Multiple(() =>
            {
                Assert.That(metricName.ToString(), Is.EqualTo("DotNet/class/method"));
                Assert.That(sameMetricName.GetHashCode(), Is.EqualTo(metricName.GetHashCode()));
                Assert.That(sameMetricName, Is.EqualTo(metricName));
            });
        }

        [Test]
        public void GetDatastoreVendorAll()
        {
            Assert.That(DatastoreVendor.MySQL.GetDatastoreVendorAll().ToString(), Is.EqualTo("Datastore/MySQL/all"));
        }

        [Test]
        public void GetDatastoreVendorAllWeb()
        {
            Assert.That(DatastoreVendor.MSSQL.GetDatastoreVendorAllWeb().ToString(), Is.EqualTo("Datastore/MSSQL/allWeb"));
        }

        [Test]
        public void GetDatastoreVendorAllOther()
        {
            Assert.That(DatastoreVendor.Oracle.GetDatastoreVendorAllOther().ToString(), Is.EqualTo("Datastore/Oracle/allOther"));
        }

        [Test]
        public void GetDatastoreOperation()
        {
            Assert.That(DatastoreVendor.MSSQL.GetDatastoreOperation("select").ToString(), Is.EqualTo("Datastore/operation/MSSQL/select"));
        }

        [Test]
        public void GetDatastoreStatement()
        {
            Assert.That(MetricNames.GetDatastoreStatement(DatastoreVendor.MySQL, "users", "select").ToString(), Is.EqualTo("Datastore/statement/MySQL/users/select"));
        }

        [Test]
        public void GetDatastoreInstance()
        {
            Assert.That(MetricNames.GetDatastoreInstance(DatastoreVendor.MSSQL, "compy64", "808").ToString(), Is.EqualTo("Datastore/instance/MSSQL/compy64/808"));
        }

        #region GetTransactionApdex

        [Test]
        public static void GetTransactionApdex_ReturnsExpectedMetricName()
        {
            var transaction = TestTransactions.CreateDefaultTransaction(true, null, null, null, null, null, "foo", "bar");
            var immutableTransaction = transaction.ConvertToImmutableTransaction();
            var transactionNameMaker = new TransactionMetricNameMaker(new MetricNameService());
            var transactionApdex = MetricNames.GetTransactionApdex(transactionNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName));

            var expectedName = "Apdex/foo/bar";

            Assert.That(transactionApdex, Is.EqualTo(expectedName));
        }

        #endregion

        [Test]
        public static void MetricNamesTest_CAT()
        {

            var testDic = new Dictionary<CATSupportabilityCondition, string>
            {
                { CATSupportabilityCondition.Request_Create_Success,                "Supportability/CrossApplicationTracing/Request/Create/Success"},
                { CATSupportabilityCondition.Request_Create_Failure,                "Supportability/CrossApplicationTracing/Request/Create/Exception"},
                { CATSupportabilityCondition.Request_Create_Failure_XProcID,        "Supportability/CrossApplicationTracing/Request/Create/Exception/CrossProcessID"},

                { CATSupportabilityCondition.Request_Accept_Success,                "Supportability/CrossApplicationTracing/Request/Accept/Success"},
                { CATSupportabilityCondition.Request_Accept_Failure,                "Supportability/CrossApplicationTracing/Request/Accept/Exception"},
                { CATSupportabilityCondition.Request_Accept_Failure_NotTrusted,     "Supportability/CrossApplicationTracing/Request/Accept/Ignored/NotTrusted" },
                { CATSupportabilityCondition.Request_Accept_Failure_Decode,         "Supportability/CrossApplicationTracing/Request/Accept/Ignored/UnableToDecode" },
                { CATSupportabilityCondition.Request_Accept_Multiple,               "Supportability/CrossApplicationTracing/Request/Accept/Warning/MultipleAttempts"},

                { CATSupportabilityCondition.Response_Create_Success,               "Supportability/CrossApplicationTracing/Response/Create/Success"},
                { CATSupportabilityCondition.Response_Create_Failure,               "Supportability/CrossApplicationTracing/Response/Create/Exception"},
                { CATSupportabilityCondition.Response_Create_Failure_XProcID,       "Supportability/CrossApplicationTracing/Response/Create/Exception/CrossProcessID"},

                { CATSupportabilityCondition.Response_Accept_Success,               "Supportability/CrossApplicationTracing/Response/Accept/Success"},
                { CATSupportabilityCondition.Response_Accept_Failure,               "Supportability/CrossApplicationTracing/Response/Accept/Exception"},
                { CATSupportabilityCondition.Response_Accept_MultipleResponses,     "Supportability/CrossApplicationTracing/Response/Accept/Ignored/MultipleAttempts"}
            };

            var assertions = new List<Action>();
            foreach (var d in testDic)
            {
                var catCond = d;
                assertions.Add(() => Assert.That(MetricNames.GetSupportabilityCATConditionMetricName(catCond.Key), Is.EqualTo(catCond.Value), $"Expected '{catCond.Value}', actual '{MetricNames.GetSupportabilityCATConditionMetricName(catCond.Key)}'"));
            }

            var countTests = testDic.Count;
            var countEnumValues = Enum.GetValues(typeof(CATSupportabilityCondition)).Length;

            assertions.Add(() => Assert.That(countTests, Is.EqualTo(countEnumValues), $"Test Coverage - there are {countEnumValues - countTests} enums missing from this test"));

            NrAssert.Multiple(assertions.ToArray());
        }



        [Test]
        public static void MetricNamesTest_DistributedTracing()
        {
            Assert.Multiple(() =>
            {
                Assert.That(MetricNames.SupportabilityDistributedTraceAcceptPayloadSuccess, Is.EqualTo("Supportability/DistributedTrace/AcceptPayload/Success"));
                Assert.That(MetricNames.SupportabilityDistributedTraceAcceptPayloadException, Is.EqualTo("Supportability/DistributedTrace/AcceptPayload/Exception"));
                Assert.That(MetricNames.SupportabilityDistributedTraceAcceptPayloadParseException, Is.EqualTo("Supportability/DistributedTrace/AcceptPayload/ParseException"));
                Assert.That(MetricNames.SupportabilityDistributedTraceAcceptPayloadIgnoredCreateBeforeAccept, Is.EqualTo("Supportability/DistributedTrace/AcceptPayload/Ignored/CreateBeforeAccept"));
                Assert.That(MetricNames.SupportabilityDistributedTraceAcceptPayloadIgnoredMultiple, Is.EqualTo("Supportability/DistributedTrace/AcceptPayload/Ignored/Multiple"));
                Assert.That(MetricNames.SupportabilityDistributedTraceAcceptPayloadIgnoredMajorVersion, Is.EqualTo("Supportability/DistributedTrace/AcceptPayload/Ignored/MajorVersion"));
                Assert.That(MetricNames.SupportabilityDistributedTraceAcceptPayloadIgnoredNull, Is.EqualTo("Supportability/DistributedTrace/AcceptPayload/Ignored/Null"));
                Assert.That(MetricNames.SupportabilityDistributedTraceAcceptPayloadIgnoredUntrustedAccount, Is.EqualTo("Supportability/DistributedTrace/AcceptPayload/Ignored/UntrustedAccount"));
                Assert.That(MetricNames.SupportabilityDistributedTraceCreatePayloadSuccess, Is.EqualTo("Supportability/DistributedTrace/CreatePayload/Success"));
                Assert.That(MetricNames.SupportabilityDistributedTraceCreatePayloadException, Is.EqualTo("Supportability/DistributedTrace/CreatePayload/Exception"));
            });
        }

        [Test]
        public static void MetricNamesTest_AgentFeatureApiVersion()
        {
            Assert.Multiple(() =>
            {
                Assert.That(MetricNames.GetSupportabilityAgentApi("method"), Is.EqualTo("Supportability/ApiInvocation/method"));
                Assert.That(MetricNames.GetSupportabilityFeatureEnabled("feature"), Is.EqualTo("Supportability/FeatureEnabled/feature"));
                Assert.That(MetricNames.GetSupportabilityAgentVersion("version"), Is.EqualTo("Supportability/AgentVersion/version"));
                Assert.That(MetricNames.GetSupportabilityAgentVersionByHost("host", "version"), Is.EqualTo("Supportability/AgentVersion/host/version"));
                Assert.That(MetricNames.GetSupportabilityLinuxOs(), Is.EqualTo("Supportability/OS/Linux"));
            });
        }

        [Test]
        public static void MetricNamesTest_AgentHealthEvent()
        {
            Assert.Multiple(() =>
            {
                Assert.That(MetricNames.GetSupportabilityAgentHealthEvent(AgentHealthEvent.TransactionGarbageCollected, null),
                            Is.EqualTo("Supportability/TransactionGarbageCollected"));
                Assert.That(MetricNames.GetSupportabilityAgentHealthEvent(AgentHealthEvent.TransactionGarbageCollected, "additional"),
                    Is.EqualTo("Supportability/TransactionGarbageCollected/additional"));
                Assert.That(MetricNames.GetSupportabilityAgentHealthEvent(AgentHealthEvent.WrapperShutdown, null),
                    Is.EqualTo("Supportability/WrapperShutdown"));
                Assert.That(MetricNames.GetSupportabilityAgentHealthEvent(AgentHealthEvent.WrapperShutdown, "additional"),
                    Is.EqualTo("Supportability/WrapperShutdown/additional"));
            });
        }

        [Test]
        public static void MetricNamesTest_Errors()
        {
            Assert.Multiple(() =>
            {
                Assert.That(MetricNames.SupportabilityErrorTracesSent,
                            Is.EqualTo("Supportability/Errors/TotalErrorsSent"));
                Assert.That(MetricNames.SupportabilityErrorTracesCollected,
                    Is.EqualTo("Supportability/Errors/TotalErrorsCollected"));
                Assert.That(MetricNames.SupportabilityErrorTracesRecollected,
                    Is.EqualTo("Supportability/Errors/TotalErrorsRecollected"));
            });
        }

        [Test]
        public static void MetricNamesTest_Utilization()
        {
            Assert.Multiple(() =>
            {
                Assert.That(MetricNames.GetSupportabilityBootIdError(), Is.EqualTo("Supportability/utilization/boot_id/error"));
                Assert.That(MetricNames.GetSupportabilityAwsUsabilityError(), Is.EqualTo("Supportability/utilization/aws/error"));
                Assert.That(MetricNames.GetSupportabilityAzureUsabilityError(), Is.EqualTo("Supportability/utilization/azure/error"));
                Assert.That(MetricNames.GetSupportabilityGcpUsabilityError(), Is.EqualTo("Supportability/utilization/gcp/error"));
                Assert.That(MetricNames.GetSupportabilityPcfUsabilityError(), Is.EqualTo("Supportability/utilization/pcf/error"));
                Assert.That(MetricNames.GetSupportabilityKubernetesUsabilityError(), Is.EqualTo("Supportability/utilization/kubernetes/error"));
            });
        }

        [Test]
        public static void MetricNamesTest_SqlTraces()
        {
            Assert.Multiple(() =>
            {
                Assert.That(MetricNames.SupportabilitySqlTracesSent, Is.EqualTo("Supportability/SqlTraces/TotalSqlTracesSent"));
                Assert.That(MetricNames.SupportabilitySqlTracesCollected.ToString(), Is.EqualTo("Supportability/SqlTraces/TotalSqlTracesCollected"));
                Assert.That(MetricNames.SupportabilitySqlTracesRecollected, Is.EqualTo("Supportability/SqlTraces/TotalSqlTracesRecollected"));
            });
        }

        [Test]
        public static void MetricNamesTest_Events()
        {

            Assert.Multiple(() =>
            {
                Assert.That(MetricNames.SupportabilityErrorEventsSent, Is.EqualTo("Supportability/Events/TransactionError/Sent"));
                Assert.That(MetricNames.SupportabilityErrorEventsSeen, Is.EqualTo("Supportability/Events/TransactionError/Seen"));
                Assert.That(MetricNames.SupportabilityCustomEventsSent, Is.EqualTo("Supportability/Events/Customer/Sent"));
                Assert.That(MetricNames.SupportabilityCustomEventsSeen, Is.EqualTo("Supportability/Events/Customer/Seen"));

                Assert.That(MetricNames.SupportabilityCustomEventsCollected,
                    Is.EqualTo("Supportability/Events/Customer/TotalEventsCollected"));
                Assert.That(MetricNames.SupportabilityCustomEventsRecollected,
                    Is.EqualTo("Supportability/Events/Customer/TotalEventsRecollected"));
                Assert.That(MetricNames.SupportabilityCustomEventsReservoirResize,
                    Is.EqualTo("Supportability/Events/Customer/TryResizeReservoir"));
            });
        }

        [Test]
        public static void MetricNamesTest_Logging()
        {
            Assert.Multiple(() =>
            {
                Assert.That(MetricNames.GetLoggingMetricsLinesName(), Is.EqualTo("Logging/lines"));
                Assert.That(MetricNames.GetLoggingMetricsDeniedName(), Is.EqualTo("Logging/denied"));
                Assert.That(MetricNames.GetLoggingMetricsLinesBySeverityName("foo"), Is.EqualTo("Logging/lines/foo"));
                Assert.That(MetricNames.GetLoggingMetricsDeniedBySeverityName("foo"), Is.EqualTo("Logging/denied/foo"));
            });
        }

        [Test]
        public static void MetricNamesTest_AnalyticEvents()
        {
            Assert.Multiple(() =>
            {
                Assert.That(MetricNames.SupportabilityTransactionEventsSent,
                            Is.EqualTo("Supportability/AnalyticsEvents/TotalEventsSent"));
                Assert.That(MetricNames.SupportabilityTransactionEventsSeen,
                    Is.EqualTo("Supportability/AnalyticsEvents/TotalEventsSeen"));
                Assert.That(MetricNames.SupportabilityTransactionEventsCollected,
                    Is.EqualTo("Supportability/AnalyticsEvents/TotalEventsCollected"));
                Assert.That(MetricNames.SupportabilityTransactionEventsRecollected,
                    Is.EqualTo("Supportability/AnalyticsEvents/TotalEventsRecollected"));
                Assert.That(MetricNames.SupportabilityTransactionEventsReservoirResize,
                    Is.EqualTo("Supportability/AnalyticsEvents/TryResizeReservoir"));
            });
        }

        [Test]
        public static void MetricNamesTest_MetricHarvest()
        {
            Assert.That(MetricNames.SupportabilityMetricHarvestTransmit, Is.EqualTo("Supportability/MetricHarvest/transmit"));
        }

        [Test]
        public static void MetricNamesTest_MiscellaneousSupportability()
        {
            Assert.Multiple(() =>
            {
                Assert.That(MetricNames.SupportabilityRumHeaderRendered, Is.EqualTo("Supportability/RUM/Header"));
                Assert.That(MetricNames.SupportabilityRumFooterRendered, Is.EqualTo("Supportability/RUM/Footer"));
                Assert.That(MetricNames.SupportabilityHtmlPageRendered, Is.EqualTo("Supportability/RUM/HtmlPage"));

                Assert.That(MetricNames.SupportabilityThreadProfilingSampleCount, Is.EqualTo("Supportability/ThreadProfiling/SampleCount"));
            });
        }

        [Test]
        public static void MetricNamesTest_ThreadPoolUsageStats()
        {
            Assert.Multiple(() =>
            {
                Assert.That(MetricNames.GetThreadpoolUsageStatsName(Samplers.ThreadType.Worker, Samplers.ThreadStatus.Available), Is.EqualTo("Threadpool/Worker/Available"));
                Assert.That(MetricNames.GetThreadpoolUsageStatsName(Samplers.ThreadType.Worker, Samplers.ThreadStatus.InUse), Is.EqualTo("Threadpool/Worker/InUse"));
                Assert.That(MetricNames.GetThreadpoolUsageStatsName(Samplers.ThreadType.Completion, Samplers.ThreadStatus.Available), Is.EqualTo("Threadpool/Completion/Available"));
                Assert.That(MetricNames.GetThreadpoolUsageStatsName(Samplers.ThreadType.Completion, Samplers.ThreadStatus.InUse), Is.EqualTo("Threadpool/Completion/InUse"));
            });
        }

        [Test]
        public static void MetricNamesTest_ThreadPoolThroughputStats()
        {
            Assert.Multiple(() =>
            {
                Assert.That(MetricNames.GetThreadpoolThroughputStatsName(Samplers.ThreadpoolThroughputStatsType.Requested), Is.EqualTo("Threadpool/Throughput/Requested"));
                Assert.That(MetricNames.GetThreadpoolThroughputStatsName(Samplers.ThreadpoolThroughputStatsType.Started), Is.EqualTo("Threadpool/Throughput/Started"));
                Assert.That(MetricNames.GetThreadpoolThroughputStatsName(Samplers.ThreadpoolThroughputStatsType.QueueLength), Is.EqualTo("Threadpool/Throughput/QueueLength"));
            });
        }

        [Test]
        public static void MetricNamesTest_GetGCMetricName()
        {
            var countGCSampleTypes = Enum.GetValues(typeof(GCSampleType)).Length;

            var expectedMetricNames = new Dictionary<GCSampleType, string>
            {
                { GCSampleType.HandlesCount, "GC/Handles" },
                { GCSampleType.InducedCount, "GC/Induced" },
                { GCSampleType.PercentTimeInGc, "GC/PercentTimeInGC" },

                { GCSampleType.Gen0CollectionCount, "GC/Gen0/Collections" },
                { GCSampleType.Gen0Size, "GC/Gen0/Size" },
                { GCSampleType.Gen0Promoted, "GC/Gen0/Promoted" },

                { GCSampleType.Gen1CollectionCount, "GC/Gen1/Collections" },
                { GCSampleType.Gen1Size, "GC/Gen1/Size" },
                { GCSampleType.Gen1Promoted, "GC/Gen1/Promoted" },

                { GCSampleType.Gen2CollectionCount, "GC/Gen2/Collections" },
                { GCSampleType.Gen2Size, "GC/Gen2/Size" },
                { GCSampleType.Gen2Survived, "GC/Gen2/Survived" },

                { GCSampleType.LOHSize, "GC/LOH/Size" },
                { GCSampleType.LOHSurvived, "GC/LOH/Survived" },
            };

            //Ensure that we have covered all sample types with our tests
            Assert.That(expectedMetricNames, Has.Count.EqualTo(countGCSampleTypes));

            foreach (var sampleType in expectedMetricNames)
            {
                Assert.That(MetricNames.GetGCMetricName(sampleType.Key), Is.EqualTo(sampleType.Value));
            }
        }

        [Test]
        public static void MetricNamesTest_GetSupportabilityName()
        {
            const string metricName = "WCFClient/BindingType/BasicHttpBinding";
            Assert.That(MetricNames.GetSupportabilityName(metricName), Is.EqualTo($"Supportability/{metricName}"));
        }

        [TestCase(StatusCode.OK, ExpectedResult = "InfiniteTracing/Span/gRPC/OK")]
        [TestCase(StatusCode.Cancelled, ExpectedResult = "InfiniteTracing/Span/gRPC/CANCELLED")]
        [TestCase(StatusCode.Unknown, ExpectedResult = "InfiniteTracing/Span/gRPC/UNKNOWN")]
        [TestCase(StatusCode.InvalidArgument, ExpectedResult = "InfiniteTracing/Span/gRPC/INVALID_ARGUMENT")]
        [TestCase(StatusCode.DeadlineExceeded, ExpectedResult = "InfiniteTracing/Span/gRPC/DEADLINE_EXCEEDED")]
        [TestCase(StatusCode.NotFound, ExpectedResult = "InfiniteTracing/Span/gRPC/NOT_FOUND")]
        [TestCase(StatusCode.AlreadyExists, ExpectedResult = "InfiniteTracing/Span/gRPC/ALREADY_EXISTS")]
        [TestCase(StatusCode.PermissionDenied, ExpectedResult = "InfiniteTracing/Span/gRPC/PERMISSION_DENIED")]
        [TestCase(StatusCode.Unauthenticated, ExpectedResult = "InfiniteTracing/Span/gRPC/UNAUTHENTICATED")]
        [TestCase(StatusCode.ResourceExhausted, ExpectedResult = "InfiniteTracing/Span/gRPC/RESOURCE_EXHAUSTED")]
        [TestCase(StatusCode.FailedPrecondition, ExpectedResult = "InfiniteTracing/Span/gRPC/FAILED_PRECONDITION")]
        [TestCase(StatusCode.Aborted, ExpectedResult = "InfiniteTracing/Span/gRPC/ABORTED")]
        [TestCase(StatusCode.OutOfRange, ExpectedResult = "InfiniteTracing/Span/gRPC/OUT_OF_RANGE")]
        [TestCase(StatusCode.Unimplemented, ExpectedResult = "InfiniteTracing/Span/gRPC/UNIMPLEMENTED")]
        [TestCase(StatusCode.Internal, ExpectedResult = "InfiniteTracing/Span/gRPC/INTERNAL")]
        [TestCase(StatusCode.Unavailable, ExpectedResult = "InfiniteTracing/Span/gRPC/UNAVAILABLE")]
        [TestCase(StatusCode.DataLoss, ExpectedResult = "InfiniteTracing/Span/gRPC/DATA_LOSS")]
        [TestCase((StatusCode)(-1), ExpectedResult = "InfiniteTracing/Span/gRPC/-1")]
        public string MetricNamesTest_SupportabilityInfiniteTracingSpanGrpcError(StatusCode statusCode)
        {
            return MetricNames.SupportabilityInfiniteTracingSpanGrpcError(EnumNameCache<StatusCode>.GetNameToUpperSnakeCase(statusCode));
        }

        // The supportability prefix is added automatically by the call to GetSupportabilityName
        [TestCase(true, ExpectedResult = "InfiniteTracing/Compression/enabled")]
        [TestCase(false, ExpectedResult = "InfiniteTracing/Compression/disabled")]
        public string MetricNamesTest_SupportabilityInfiniteTracingCompression(bool compressionEnabled)
        {
            return MetricNames.SupportabilityInfiniteTracingCompression(compressionEnabled);
        }
    }
}
