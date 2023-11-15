// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.DistributedTracing
{
    [NetCoreTest]
    public class AspNetCoreDistTraceRequestChainTests : NewRelicIntegrationTest<AspNetCoreDistTraceRequestChainFixture>
    {
        private readonly AspNetCoreDistTraceRequestChainFixture _fixture;

        private const int ExpectedTransactionCount = 2;
        public const string TransportType = "HTTP"; //All calls are Http for the distributed traces

        public AspNetCoreDistTraceRequestChainTests(AspNetCoreDistTraceRequestChainFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    //Important setup happens in fixture
                },
                exerciseApplication: () =>
                {
                    _fixture.ExecuteTraceRequestChain("CallNext", "CallNext", "CallEnd", null);
                    _fixture.ExecuteTraceRequestChain("CallNext", "CallNext", "CallError", null);

                    _fixture.FirstCallApplication.AgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromSeconds(15), ExpectedTransactionCount);
                    _fixture.SecondCallApplication.AgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromSeconds(15), ExpectedTransactionCount);
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromSeconds(15), ExpectedTransactionCount);
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void VerifyFirstApplication()
        {
            var metrics = _fixture.FirstCallApplication.AgentLog.GetMetrics().ToList();

            Assert.NotNull(metrics);

            Assertions.MetricsExist(_generalMetrics, metrics);

            NrAssert.Multiple(
                () => Assertions.MetricsExist(FirstAppExpectedData.DistributedTraceSupportabilityMetrics, metrics),
                () => Assertions.MetricsExist(FirstAppExpectedData.DistributedTracingMetrics, metrics)
            );

            Assertions.MetricsExist(FirstAppExpectedData.CallNextMetrics, metrics);

            var transactionEvents = _fixture.FirstCallApplication.AgentLog.GetTransactionEvents().ToList();

            Assert.Equal(ExpectedTransactionCount, transactionEvents.Count);

            AssertFirstAppTransactionEvent(0);
            AssertFirstAppTransactionEvent(1);

            void AssertFirstAppTransactionEvent(int eventIndex)
            {
                var transactionEvent = transactionEvents[eventIndex];

                NrAssert.Multiple(
                    () => Assertions.TransactionEventHasAttributes(_expectedAttributeValuesAllApps, TransactionEventAttributeType.Intrinsic, transactionEvent),
                    () => Assertions.TransactionEventHasAttributes(_expectedAttributesAllApps, TransactionEventAttributeType.Intrinsic, transactionEvent)
                );
            }

            var allSpanEvents = _fixture.FirstCallApplication.AgentLog.GetSpanEvents()
                .OrderBy(s => s.IntrinsicAttributes["timestamp"]).ToList();

            ValidateSpanEventAttributesForTransactionEvent(0);
            ValidateSpanEventAttributesForTransactionEvent(1);

            void ValidateSpanEventAttributesForTransactionEvent(int eventIndex)
            {
                var transactionId = transactionEvents[eventIndex].IntrinsicAttributes["guid"].ToString();
                var traceId = transactionEvents[eventIndex].IntrinsicAttributes["traceId"].ToString();
                ValidateSpanEventAttributesAllApps(allSpanEvents, transactionId);
                ValidateSpanEventAttributesFirstApp(allSpanEvents, transactionId, traceId);
            }

            Assertions.MetricsExist(FirstAppExpectedData.SpanEventSupportMetrics, metrics);
        }

        [Fact]
        public void VerifySecondApplication()
        {
            var metrics = _fixture.SecondCallApplication.AgentLog.GetMetrics().ToList();
            var accountId = _fixture.AgentLog.GetAccountId();
            var secondAppExpectedData = new SecondAppExpectedData(accountId);

            Assert.NotNull(metrics);

            Assertions.MetricsExist(_generalMetrics, metrics);

            NrAssert.Multiple(
                () => Assertions.MetricsExist(SecondAppExpectedData.DistributedTraceSupportabilityMetrics, metrics),
                () => Assertions.MetricsExist(SecondAppExpectedData.DistributedTracingMetrics, metrics)
            );

            Assertions.MetricsExist(SecondAppExpectedData.CallNextMetrics, metrics);

            var transactionEvents = _fixture.SecondCallApplication.AgentLog.GetTransactionEvents()
                .OrderBy(evt => evt.IntrinsicAttributes["timestamp"])
                .ToList();

            Assert.Equal(ExpectedTransactionCount, transactionEvents.Count);

            AssertSecondAppTransactionEvent(0);
            AssertSecondAppTransactionEvent(1);

            var parentTransactionEvent = _fixture.FirstCallApplication.AgentLog.GetTransactionEvents()
                .OrderBy(evt => evt.IntrinsicAttributes["timestamp"]).ToList().First();

            Assert.True(TraceIdsAreEqual(parentTransactionEvent, transactionEvents.First()));

            void AssertSecondAppTransactionEvent(int eventIndex)
            {
                var expectedPersistedParentEventAttributes = GetExpectedTransactionAttributesFromPreviousApp(_fixture.FirstCallApplication, eventIndex);
                var transactionEvent = transactionEvents[eventIndex];

                NrAssert.Multiple(
                    () => Assertions.TransactionEventHasAttributes(_expectedAttributeValuesAllApps, TransactionEventAttributeType.Intrinsic, transactionEvent),
                    () => Assertions.TransactionEventHasAttributes(GetExpectedAttributeValuesPayloadReceived(accountId), TransactionEventAttributeType.Intrinsic, transactionEvent),
                    () => Assertions.TransactionEventHasAttributes(_expectedAttributesAllApps, TransactionEventAttributeType.Intrinsic, transactionEvent),
                    () => Assertions.TransactionEventHasAttributes(_expectedAttributesPayloadReceived, TransactionEventAttributeType.Intrinsic, transactionEvent),
                    () => Assertions.TransactionEventHasAttributes(expectedPersistedParentEventAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent)
                );
            }

            var allSpanEvents = _fixture.SecondCallApplication.AgentLog.GetSpanEvents()
                .OrderBy(s => s.IntrinsicAttributes["timestamp"]).ToList();

            ValidateSpanEventAttributesForTransactionEvent(0);
            ValidateSpanEventAttributesForTransactionEvent(1);

            void ValidateSpanEventAttributesForTransactionEvent(int eventIndex)
            {
                var transactionId = transactionEvents[eventIndex].IntrinsicAttributes["guid"].ToString();
                var expectedTraceId =
                    _fixture.FirstCallApplication.AgentLog.GetTransactionEvents()
                        .OrderBy(e => e.IntrinsicAttributes["timestamp"]).ToList()[eventIndex]
                        .IntrinsicAttributes["traceId"].ToString();

                ValidateSpanEventAttributesAllApps(allSpanEvents, transactionId);
                ValidateSpanEventAttributesChildApp(allSpanEvents, transactionId, expectedTraceId);
            }

            Assertions.MetricsExist(SecondAppExpectedData.SpanEventSupportMetrics, metrics);
        }

        [Fact]
        public void VerifyFinalApplication()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var accountId = _fixture.AgentLog.GetAccountId();
            var finalAppExpectedData = new FinalAppExpectedData(accountId);

            Assert.NotNull(metrics);

            Assertions.MetricsExist(_generalMetrics, metrics);

            NrAssert.Multiple(
                () => Assertions.MetricsExist(FinalAppExpectedData.DistributedTraceSupportabilityMetrics, metrics),
                () => Assertions.MetricsExist(FinalAppExpectedData.DistributedTracingMetrics, metrics)
            );

            NrAssert.Multiple(
                () => Assertions.MetricsExist(FinalAppExpectedData.CallEndMetrics, metrics),
                () => Assertions.MetricsExist(FinalAppExpectedData.CallErrorMetrics, metrics)
            );

            var transactionEvents = _fixture.AgentLog.GetTransactionEvents()
                .OrderBy(evt => evt.IntrinsicAttributes["timestamp"])
                .ToList();

            Assert.Equal(ExpectedTransactionCount, transactionEvents.Count);

            AssertFinalAppTransactionEvent(0);
            AssertFinalAppTransactionEvent(1);

            var parentTransactionEvent = _fixture.SecondCallApplication.AgentLog.GetTransactionEvents()
                .OrderBy(evt => evt.IntrinsicAttributes["timestamp"])
                .ToList()
                .First();

            Assert.True(TraceIdsAreEqual(parentTransactionEvent, transactionEvents.First()));

            void AssertFinalAppTransactionEvent(int eventIndex)
            {
                var expectedPersistedParentEventAttributes = GetExpectedTransactionAttributesFromPreviousApp(_fixture.SecondCallApplication, eventIndex);
                var expectedAttributeValuesPayloadReceived = GetExpectedAttributeValuesPayloadReceived(_fixture.SecondCallApplication.AgentLog.GetAccountId());
                var transactionEvent = transactionEvents[eventIndex];

                try
                {
                    NrAssert.Multiple(
                        () => Assertions.TransactionEventHasAttributes(_expectedAttributeValuesAllApps, TransactionEventAttributeType.Intrinsic, transactionEvent),
                        () => Assertions.TransactionEventHasAttributes(expectedAttributeValuesPayloadReceived, TransactionEventAttributeType.Intrinsic, transactionEvent),
                        () => Assertions.TransactionEventHasAttributes(_expectedAttributesAllApps, TransactionEventAttributeType.Intrinsic, transactionEvent),
                        () => Assertions.TransactionEventHasAttributes(_expectedAttributesPayloadReceived, TransactionEventAttributeType.Intrinsic, transactionEvent),
                        () => Assertions.TransactionEventHasAttributes(expectedPersistedParentEventAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent)
                    );
                }
                catch (TestFailureException ex)
                {
                    throw new TestFailureException($"Assertion failures for transactionEvent at index {eventIndex}: {ex.Message}");
                }
            }

            var allSpanEvents = _fixture.AgentLog.GetSpanEvents()
                .OrderBy(s => s.IntrinsicAttributes["timestamp"]).ToList();

            ValidateSpanEventAttributesForTransactionEvent(0);
            ValidateSpanEventAttributesForTransactionEvent(1);

            void ValidateSpanEventAttributesForTransactionEvent(int eventIndex)
            {
                var transactionId = transactionEvents[eventIndex].IntrinsicAttributes["guid"].ToString();
                var expectedTraceId =
                    _fixture.FirstCallApplication.AgentLog.GetTransactionEvents()
                        .OrderBy(e => e.IntrinsicAttributes["timestamp"]).ToList()[eventIndex]
                        .IntrinsicAttributes["traceId"].ToString();

                ValidateSpanEventAttributesAllApps(allSpanEvents, transactionId);
                ValidateSpanEventAttributesChildApp(allSpanEvents, transactionId, expectedTraceId);
            }

            Assertions.MetricsExist(FinalAppExpectedData.SpanEventSupportMetrics, metrics);
        }

        #region Transaction Event Validation

        private static Dictionary<string, object> GetExpectedTransactionAttributesFromPreviousApp(RemoteApplication application, int index)
        {
            var parentAccountId = application.AgentLog.GetAccountId();
            var transactionEvents = application.AgentLog.GetTransactionEvents()
                .OrderBy(evt => evt.IntrinsicAttributes["timestamp"])
                .ToList();

            Assert.True(transactionEvents.Count > index, $"Previous app does not have enough Transaction Events for the provided index. Events: {transactionEvents.Count} | index: {index}");

            var transactionEvent = transactionEvents[index];

            var failedKeys = new List<string>();
            var attributes = new Dictionary<string, object>();

            if (transactionEvent.IntrinsicAttributes.ContainsKey("priority"))
            {
                attributes.Add("priority", transactionEvent.IntrinsicAttributes["priority"]);
            }
            else
            {
                failedKeys.Add("priority");
            }

            if (transactionEvent.IntrinsicAttributes.ContainsKey("sampled"))
            {
                attributes.Add("sampled", transactionEvent.IntrinsicAttributes["sampled"]);
            }
            else
            {
                failedKeys.Add("sampled");
            }

            Assert.True(failedKeys.Count == 0, $"Previous app's first Transaction Event did not contain the keys: \"{string.Join(", ", failedKeys)}\"");

            return attributes;
        }

        private readonly List<Assertions.ExpectedMetric> _generalMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Supportability/OS/Linux", callCount = 1 }, //1 per harvest
			new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsCollected", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"Apdex"},
            new Assertions.ExpectedMetric { metricName = @"ApdexAll"},
            new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"WebTransaction", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", CallCountAllHarvests = ExpectedTransactionCount },

            new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", CallCountAllHarvests = ExpectedTransactionCount }
        };

        private readonly Dictionary<string, object> _expectedAttributeValuesAllApps = new Dictionary<string, object>
        {
            {"sampled", true}
        };

        private Dictionary<string, object> GetExpectedAttributeValuesPayloadReceived(string accountId)
        {
            return new Dictionary<string, object>
            {
                {"parent.type", "App"},
                {"parent.account", accountId},
                {"parent.transportType", TransportType}
           };
        }

        private readonly List<string> _expectedAttributesAllApps = new List<string>
        {
            "guid",
            "traceId",
            "priority"
        };

        private readonly List<string> _expectedAttributesPayloadReceived = new List<string>
        {
            "parent.app",
            "parent.transportDuration",
            "parentId",
            "parentSpanId"
        };

        private bool TraceIdsAreEqual(TransactionEvent parentTransactionEvent, TransactionEvent childTransactionEvent)
        {
            var parentTraceIdAttributeValue = (string)parentTransactionEvent.IntrinsicAttributes["traceId"];
            var childTraceIdAttributeValue = (string)childTransactionEvent.IntrinsicAttributes["traceId"];

            if (parentTraceIdAttributeValue != null && childTraceIdAttributeValue != null)
            {
                return parentTraceIdAttributeValue.Equals(childTraceIdAttributeValue);
            }

            return false;
        }

        public class FirstAppExpectedData
        {
            private const int ExpectedSpanEventCount = 8;

            public static readonly List<Assertions.ExpectedMetric> DistributedTraceSupportabilityMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/CreatePayload/Success", CallCountAllHarvests = ExpectedTransactionCount },
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/Create/Success", CallCountAllHarvests = ExpectedTransactionCount }
            };

            public static readonly List<Assertions.ExpectedMetric> DistributedTracingMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"DurationByCaller/Unknown/Unknown/Unknown/HTTP/all", IsRegexName = false, CallCountAllHarvests = ExpectedTransactionCount},
                new Assertions.ExpectedMetric { metricName = @"DurationByCaller/Unknown/Unknown/Unknown/HTTP/allWeb", IsRegexName = false, CallCountAllHarvests = ExpectedTransactionCount}
            };

            public static readonly List<Assertions.ExpectedMetric> CallNextMetrics = new List<Assertions.ExpectedMetric> {
                new Assertions.ExpectedMetric { metricName = @"Apdex/MVC/FirstCall/CallNext/{nextUrl}"},
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/FirstCall/CallNext/{nextUrl}", CallCountAllHarvests = ExpectedTransactionCount },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/MVC/FirstCall/CallNext/{nextUrl}", CallCountAllHarvests = ExpectedTransactionCount },

                new Assertions.ExpectedMetric { metricName = @"DotNet/FirstCallController/CallNext", CallCountAllHarvests = ExpectedTransactionCount },
                new Assertions.ExpectedMetric { metricName = $@"External/{RemoteApplication.DestinationServerName}/Stream/GET", CallCountAllHarvests = ExpectedTransactionCount },

                new Assertions.ExpectedMetric { metricName = @"DotNet/FirstCallController/CallNext", metricScope = @"WebTransaction/MVC/FirstCall/CallNext/{nextUrl}", CallCountAllHarvests = ExpectedTransactionCount },
                new Assertions.ExpectedMetric { metricName = $@"External/{RemoteApplication.DestinationServerName}/Stream/GET", metricScope = @"WebTransaction/MVC/FirstCall/CallNext/{nextUrl}", CallCountAllHarvests = ExpectedTransactionCount },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", metricScope = @"WebTransaction/MVC/FirstCall/CallNext/{nextUrl}", CallCountAllHarvests = ExpectedTransactionCount },
            };

            public static readonly List<Assertions.ExpectedMetric> SpanEventSupportMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/SpanEvent/TotalEventsSeen", CallCountAllHarvests = ExpectedSpanEventCount }
            };
        }

        public class SecondAppExpectedData
        {
            private const int ExpectedSpanEventCount = 8;
            private static string _accountId;

            public SecondAppExpectedData(string accountId)
            {
                _accountId = accountId;
            }

            public static readonly List<Assertions.ExpectedMetric> DistributedTraceSupportabilityMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/Accept/Success", CallCountAllHarvests = ExpectedTransactionCount },
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/Create/Success", CallCountAllHarvests = ExpectedTransactionCount },
                new Assertions.ExpectedMetric { metricName = @"Supportability/DistributedTrace/CreatePayload/Success", CallCountAllHarvests = ExpectedTransactionCount }
            };

            public static List<Assertions.ExpectedMetric> DistributedTracingMetrics => new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $@"^DurationByCaller/App/{_accountId}/[^/]+/{TransportType}/all$", IsRegexName = true, CallCountAllHarvests = ExpectedTransactionCount},
                new Assertions.ExpectedMetric { metricName = $@"^DurationByCaller/App/{_accountId}/[^/]+/{TransportType}/allWeb$", IsRegexName = true, CallCountAllHarvests = ExpectedTransactionCount},

                new Assertions.ExpectedMetric { metricName = $@"^TransportDuration/App/{_accountId}/[^/]+/{TransportType}/all$", IsRegexName = true, CallCountAllHarvests = ExpectedTransactionCount},
                new Assertions.ExpectedMetric { metricName = $@"^TransportDuration/App/{_accountId}/[^/]+/{TransportType}/allWeb$", IsRegexName = true, CallCountAllHarvests = ExpectedTransactionCount}
            };

            public static readonly List<Assertions.ExpectedMetric> CallNextMetrics = new List<Assertions.ExpectedMetric> {
                new Assertions.ExpectedMetric { metricName = @"Apdex/MVC/SecondCall/CallNext/{nextUrl}"},
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/SecondCall/CallNext/{nextUrl}", CallCountAllHarvests = ExpectedTransactionCount },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/MVC/SecondCall/CallNext/{nextUrl}", CallCountAllHarvests = ExpectedTransactionCount },

                new Assertions.ExpectedMetric { metricName = @"DotNet/SecondCallController/CallNext", CallCountAllHarvests = ExpectedTransactionCount },
                new Assertions.ExpectedMetric { metricName = $@"External/{RemoteApplication.DestinationServerName}/Stream/GET", CallCountAllHarvests = ExpectedTransactionCount },

                new Assertions.ExpectedMetric { metricName = @"DotNet/SecondCallController/CallNext", metricScope = @"WebTransaction/MVC/SecondCall/CallNext/{nextUrl}", CallCountAllHarvests = ExpectedTransactionCount },
                new Assertions.ExpectedMetric { metricName = $@"External/{RemoteApplication.DestinationServerName}/Stream/GET", metricScope = @"WebTransaction/MVC/SecondCall/CallNext/{nextUrl}", CallCountAllHarvests = ExpectedTransactionCount },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", metricScope = @"WebTransaction/MVC/SecondCall/CallNext/{nextUrl}", CallCountAllHarvests = ExpectedTransactionCount },
            };

            public static readonly List<Assertions.ExpectedMetric> SpanEventSupportMetrics = new List<Assertions.ExpectedMetric> {
                new Assertions.ExpectedMetric { metricName = @"Supportability/SpanEvent/TotalEventsSeen", CallCountAllHarvests = ExpectedSpanEventCount }
            };
        }

        public class FinalAppExpectedData
        {
            private const int ExpectedSpanEventCount = 7;
            private static string _accountId;

            public FinalAppExpectedData(string accountId)
            {
                _accountId = accountId;
            }

            public static readonly List<Assertions.ExpectedMetric> DistributedTraceSupportabilityMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/TraceContext/Accept/Success", CallCountAllHarvests = ExpectedTransactionCount }
            };

            public static List<Assertions.ExpectedMetric> DistributedTracingMetrics => new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $@"^DurationByCaller/App/{_accountId}/[^/]+/{TransportType}/all$", IsRegexName = true, CallCountAllHarvests = ExpectedTransactionCount},
                new Assertions.ExpectedMetric { metricName = $@"^DurationByCaller/App/{_accountId}/[^/]+/{TransportType}/allWeb$", IsRegexName = true, CallCountAllHarvests = ExpectedTransactionCount},

                new Assertions.ExpectedMetric { metricName = $@"^TransportDuration/App/{_accountId}/[^/]+/{TransportType}/all$", IsRegexName = true, CallCountAllHarvests = ExpectedTransactionCount},
                new Assertions.ExpectedMetric { metricName = $@"^TransportDuration/App/{_accountId}/[^/]+/{TransportType}/allWeb$", IsRegexName = true, CallCountAllHarvests = ExpectedTransactionCount},

                new Assertions.ExpectedMetric { metricName = $@"^ErrorsByCaller/App/{_accountId}/[^/]+/{TransportType}/all$", IsRegexName = true, CallCountAllHarvests = 1},
                new Assertions.ExpectedMetric { metricName = $@"^ErrorsByCaller/App/{_accountId}/[^/]+/{TransportType}/allWeb$", IsRegexName = true, CallCountAllHarvests = 1}
            };

            public static readonly List<Assertions.ExpectedMetric> CallEndMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Apdex/MVC/LastCall/CallEnd"},
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/LastCall/CallEnd", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/MVC/LastCall/CallEnd", CallCountAllHarvests = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/LastCallController/CallEnd", CallCountAllHarvests = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/LastCallController/CallEnd", metricScope = @"WebTransaction/MVC/LastCall/CallEnd", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", metricScope = @"WebTransaction/MVC/LastCall/CallEnd", CallCountAllHarvests = 1 },
            };

            public static readonly List<Assertions.ExpectedMetric> CallErrorMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Apdex/MVC/LastCall/CallError"},
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/LastCall/CallError", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/MVC/LastCall/CallError", CallCountAllHarvests = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/LastCallController/CallError", CallCountAllHarvests = 1 },

                new Assertions.ExpectedMetric { metricName = @"DotNet/LastCallController/CallError", metricScope = @"WebTransaction/MVC/LastCall/CallError", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", metricScope = @"WebTransaction/MVC/LastCall/CallError", CallCountAllHarvests = 1 },
            };

            public static readonly List<Assertions.ExpectedMetric> SpanEventSupportMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/SpanEvent/TotalEventsSeen", CallCountAllHarvests = ExpectedSpanEventCount }
            };
    }

    #endregion

    #region Span Event Validation

    private readonly List<string> _expectedSpanEventAttributesAllApps = new List<string>()
        {
            "type",
            "traceId",
            "guid",
            "transactionId",
            "sampled",
            "priority",
            "timestamp",
            "duration",
            "name",
            "category",
        };

        private readonly List<string> _expectedSpanEventAttributesFirstSpan = new List<string>()
        {
            "nr.entryPoint"
        };

        private readonly List<string> _unexpectedSpanEventAttributesFirstSpan = new List<string>()
        {
            "parentId"
        };

        private void ValidateSpanEventAttributesAllApps(List<SpanEvent> allSpanEvents, string transactionId)
        {
            Assert.True(allSpanEvents.Count > 0);

            var spanEventsForTransaction = allSpanEvents.Where(s => s.IntrinsicAttributes["transactionId"].Equals(transactionId)).ToList();

            for (var x = 0; x < spanEventsForTransaction.Count; x++)
            {
                var isFirstSpanEvent = (x == 0) ? true : false;
                AssertOnSpanEventAttributes(x, isFirstSpanEvent);
            }

            void AssertOnSpanEventAttributes(int eventIndex, bool isFirstSpan)
            {
                var currentSpan = spanEventsForTransaction[eventIndex];

                if (isFirstSpan)
                {
                    NrAssert.Multiple(
                        () => Assertions.SpanEventHasAttributes(_expectedSpanEventAttributesAllApps, SpanEventAttributeType.Intrinsic, currentSpan),
                        () => Assertions.SpanEventHasAttributes(_expectedSpanEventAttributesFirstSpan, SpanEventAttributeType.Intrinsic, currentSpan),
                        () => Assert.True((bool)currentSpan.IntrinsicAttributes["nr.entryPoint"]),
                        () => Assert.Equal(transactionId, currentSpan.IntrinsicAttributes["transactionId"])
                    );
                }
                else
                {
                    NrAssert.Multiple(
                        () => Assertions.SpanEventHasAttributes(_expectedSpanEventAttributesAllApps, SpanEventAttributeType.Intrinsic, currentSpan),
                        () => Assertions.SpanEventDoesNotHaveAttributes(_expectedSpanEventAttributesFirstSpan, SpanEventAttributeType.Intrinsic, currentSpan),
                        () => Assert.Equal(transactionId, currentSpan.IntrinsicAttributes["transactionId"])
                    );
                }
            }
        }

        private void ValidateSpanEventAttributesFirstApp(List<SpanEvent> allSpanEvents, string transactionId, string expectedTraceId)
        {
            Assert.True(allSpanEvents.Count > 0);

            var spanEventsForTransaction = allSpanEvents.Where(s => s.IntrinsicAttributes["transactionId"].Equals(transactionId)).ToList();

            for (var x = 0; x < spanEventsForTransaction.Count; x++)
            {
                var isFirstSpanEvent = (x == 0) ? true : false;
                AssertOnSpanEventAttributesFirstApp(x, isFirstSpanEvent);
            }

            void AssertOnSpanEventAttributesFirstApp(int eventIndex, bool isFirstSpan)
            {
                var currentSpan = spanEventsForTransaction[eventIndex];

                if (isFirstSpan)
                {
                    NrAssert.Multiple(
                        () => Assertions.SpanEventDoesNotHaveAttributes(_unexpectedSpanEventAttributesFirstSpan, SpanEventAttributeType.Intrinsic, currentSpan),
                        () => Assert.Equal(expectedTraceId, currentSpan.IntrinsicAttributes["traceId"])

                    );
                }
                else
                {
                    NrAssert.Multiple(
                        () => Assert.Equal(expectedTraceId, currentSpan.IntrinsicAttributes["traceId"])
                    );
                }
            }
        }

        private void ValidateSpanEventAttributesChildApp(List<SpanEvent> allSpanEvents, string transactionId, string expectedTraceId)
        {
            Assert.True(allSpanEvents.Count > 0);

            var spanEventsForTransaction = allSpanEvents.Where(s => s.IntrinsicAttributes["transactionId"].Equals(transactionId)).ToList();

            for (var x = 0; x < spanEventsForTransaction.Count; x++)
            {
                var isFirstSpanEvent = (x == 0) ? true : false;
                AssertOnSpanEventAttributesFirstApp(x, isFirstSpanEvent);
            }

            void AssertOnSpanEventAttributesFirstApp(int eventIndex, bool isFirstSpan)
            {
                var currentSpan = spanEventsForTransaction[eventIndex];

                if (isFirstSpan)
                {
                    Assert.Equal(expectedTraceId, currentSpan.IntrinsicAttributes["traceId"]);
                }
                else
                {
                    var currentSpanId = currentSpan.IntrinsicAttributes["guid"].ToString();
                    var filteredSpanIds = spanEventsForTransaction.Select(s => s.IntrinsicAttributes["guid"].ToString()).Where(id => !id.Equals(currentSpanId)).ToList();

                    NrAssert.Multiple(
                        () => Assert.Contains(currentSpan.IntrinsicAttributes["parentId"].ToString(), filteredSpanIds),
                        () => Assert.Equal(expectedTraceId, currentSpan.IntrinsicAttributes["traceId"])
                    );
                }
            }
        }

        #endregion
    }
}
