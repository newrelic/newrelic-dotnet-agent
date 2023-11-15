// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK

using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using NewRelic.Testing.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.WCF.Client
{
    public abstract class WCFClientTestBase : WCFLegacyTestBase
    {
        private static readonly WCFInvocationMethod[] _instrumentedClientInvocMethods = Enum.GetValues(typeof(WCFInvocationMethod)).Cast<WCFInvocationMethod>().ToArray();

        protected override int _expectedTransactionCount_Client => _countClientInvocationMethodsToTest * COUNT_SVC_METHODS;     //2 methods being called (getdata, throwException)
        protected override int _expectedTransactionCount_Service => _countClientInvocationMethodsToTest * COUNT_SVC_METHODS;    //2 methods being called (getdata, throwException)
        protected bool _thereWereCATFailures => LogHelpers.TrxTripIDs_Client.Except(LogHelpers.TrxIDs_Client).Any();

        public WCFClientTestBase(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output, WCFBindingType bindingToTest, TracingTestOption tracingTestOption, HostingModel hostingTestOption, ASPCompatibilityMode aspCompatModeOption, IWCFLogHelpers logHelpersImpl)
            : base(fixture, output, bindingToTest, _instrumentedClientInvocMethods, new[] { WCFInvocationMethod.Sync }, tracingTestOption, hostingTestOption, aspCompatModeOption, logHelpersImpl)
        {
        }

        [Fact]
        public override void TransactionEvents()
        {
            Assert.True(LogHelpers.TrxEvents.Length > 0, "Unable to obtain Transaction Events from log.");
            Assert.True(_expectedTransactionCount_Client == LogHelpers.TrxEvents_Client.Length, $"Client Transaction Count - Expected {_expectedTransactionCount_Client}, Actual {LogHelpers.TrxEvents_Client.Length}");
        }

        [Fact]
        public override void Metrics()
        {
            var serverName = _bindingToTest == WCFBindingType.NetTcp ? "127.0.0.1" :  "localhost";

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric(){ metricName = $"External/{serverName}/Stream/{SharedWcfLibraryNamespace}.IWcfClient.Sync_SyncThrowException", callCount = 2 },
                new Assertions.ExpectedMetric(){ metricName = $"External/{serverName}/Stream/{SharedWcfLibraryNamespace}.IWcfClient.Begin_SyncGetData", callCount = 2 /*Begin/End + Event Based Async*/  },
                new Assertions.ExpectedMetric(){ metricName = $"External/{serverName}/Stream/{SharedWcfLibraryNamespace}.IWcfClient.Begin_SyncThrowException", callCount = 4 /*Begin/End + Event Based Async*/ },
                new Assertions.ExpectedMetric(){ metricName = $"External/{serverName}/Stream/{SharedWcfLibraryNamespace}.IWcfClient.TAP_SyncGetData" , callCount = 1 },
                new Assertions.ExpectedMetric(){ metricName = $"External/{serverName}/Stream/{SharedWcfLibraryNamespace}.IWcfClient.TAP_SyncThrowException", callCount = 2  },
                new Assertions.ExpectedMetric(){ metricName = $"External/{serverName}/Stream/{SharedWcfLibraryNamespace}.IWcfClient.Sync_SyncGetData", callCount = 1  },

                new Assertions.ExpectedMetric(){ metricName = "DotNet/ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF.WCFClient/ThrowException",
                    metricScope = "OtherTransaction/Custom/ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF.WCFClient/ThrowException", callCount = _countClientInvocationMethodsToTest * 2 },
                new Assertions.ExpectedMetric(){ metricName = "DotNet/ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF.WCFClient/GetData",
                    metricScope = "OtherTransaction/Custom/ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF.WCFClient/GetData", callCount = _countClientInvocationMethodsToTest },
                new Assertions.ExpectedMetric(){ metricName = "DotNet/ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF.WCFClient/GetData", callCount = _countClientInvocationMethodsToTest },

                new Assertions.ExpectedMetric(){ metricName = "OtherTransaction/Custom/ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF.WCFClient/ThrowException", callCount = _countClientInvocationMethodsToTest * 2 },
                new Assertions.ExpectedMetric(){ metricName = "OtherTransaction/Custom/ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF.WCFClient/GetData", callCount = _countClientInvocationMethodsToTest },
            };

            var catExcludedMetrics = new[]
            {
                new Assertions.ExpectedMetric() { metricName = $"External/{serverName}/Stream/{SharedWcfLibraryNamespace}.IWcfClient.Begin_SyncGetData",
                    metricScope = "OtherTransaction/Custom/ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF.WCFClient/GetData" },
                new Assertions.ExpectedMetric() { metricName = $"External/{serverName}/Stream/{SharedWcfLibraryNamespace}.IWcfClient.TAP_SyncGetData",
                    metricScope = "OtherTransaction/Custom/ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF.WCFClient/GetData" },
                new Assertions.ExpectedMetric() { metricName = $"External/{serverName}/Stream/{SharedWcfLibraryNamespace}.IWcfClient.Sync_SyncGetData",
                    metricScope = "OtherTransaction/Custom/ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF.WCFClient/GetData" },
                new Assertions.ExpectedMetric() { metricName = $"External/{serverName}/Stream/{SharedWcfLibraryNamespace}.IWcfClient.Sync_SyncThrowException",
                    metricScope = "OtherTransaction/Custom/ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF.WCFClient/ThrowException" },
                new Assertions.ExpectedMetric() { metricName = $"External/{serverName}/Stream/{SharedWcfLibraryNamespace}.IWcfClient.Begin_SyncThrowException",
                    metricScope = "OtherTransaction/Custom/ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF.WCFClient/ThrowException" },
                new Assertions.ExpectedMetric() { metricName = $"External/{serverName}/Stream/{SharedWcfLibraryNamespace}.IWcfClient.TAP_SyncThrowException",
                    metricScope = "OtherTransaction/Custom/ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF.WCFClient/ThrowException" },
            };

            var assertions = new List<Action>();
            assertions.Add(() => Assertions.MetricsExist(expectedMetrics, LogHelpers.MetricValues));

            //If there are instances where client side continuation does not complete prior to the completion of the
            //transaction, these assertions cannot be made.
            if (!_thereWereCATFailures)
            {
                if (_tracingTestOption == TracingTestOption.CAT)
                {
                    assertions.Add(() => Assertions.MetricsDoNotExist(catExcludedMetrics, LogHelpers.MetricValues));
                }
                else
                {
                    assertions.Add(() => Assertions.MetricsExist(catExcludedMetrics, LogHelpers.MetricValues));
                }
            }

            NrAssert.Multiple(assertions.ToArray());
        }

        [Fact]
        public override void DistributedTracing_Metrics()
        {
            if (_tracingTestOption != TracingTestOption.DT)
            {
                return;
            }

            var acctId = _fixture.AgentLog.GetAccountId();
            var appId = _fixture.AgentLog.GetApplicationId();

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
				// generated by client
				new Assertions.ExpectedMetric(){ metricName = "DurationByCaller/Unknown/Unknown/Unknown/Unknown/all",
                    callCount = _expectedTransactionCount_Client
                },
				// generated by service
				new Assertions.ExpectedMetric(){ metricName = $"DurationByCaller/App/{acctId}/{appId}/{ExpectedTransportType}/all",
                    callCount = _expectedTransactionCount_Client
                },
                new Assertions.ExpectedMetric(){ metricName = "Supportability/DistributedTrace/CreatePayload/Success",
                    callCount =     (_expectedTransactionCount_Client)						//Each Client Call
								+   (_countClientInvocationMethodsToTest)					//Covering the HTTP call in GetData in the service
				},
                new Assertions.ExpectedMetric(){ metricName = "Supportability/TraceContext/Create/Success",
                    callCount =     (_expectedTransactionCount_Client)						//Each Client Call
								+   (_countClientInvocationMethodsToTest)					//Covering the HTTP call in GetData in the service
				}
            };

            Assertions.MetricsExist(expectedMetrics, LogHelpers.MetricValues);
        }

        [Fact]
        public override void CAT_Metrics()
        {
            var countExpectedAccept = _hostingModelOption == HostingModel.Self || _aspCompatibilityOption == ASPCompatibilityMode.Enabled
                ? _countClientInvocationMethodsToTest * COUNT_SVC_METHODS
                : _countClientInvocationMethodsToTest * COUNT_SVC_METHODS * 2;     //second CAT Accept call for the ignored transaction
            var countExpectedResponse = _countClientInvocationMethodsToTest * COUNT_SVC_METHODS;

            var countExpectedCreate = _countClientInvocationMethodsToTest * COUNT_SVC_METHODS      //1 for each WCF Client call
                                    + _countClientInvocationMethodsToTest;                         //1 for GetData's HTTP call on the service

            var serverName = _bindingToTest == WCFBindingType.NetTcp ? "127.0.0.1" :  "localhost";

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric(){ callCount = _countClientInvocationMethodsToTest * COUNT_SVC_METHODS, metricName = $"ExternalApp/{serverName}/{CATCrossProcessID_Service}/all" },
                new Assertions.ExpectedMetric(){ callCount = _countClientInvocationMethodsToTest, metricName = $"ExternalTransaction/{serverName}/{CATCrossProcessID_Service}/WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.SyncGetData"},
                new Assertions.ExpectedMetric(){ callCount = _countClientInvocationMethodsToTest, metricName = $"ExternalTransaction/{serverName}/{CATCrossProcessID_Service}/WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.SyncGetData",
                    metricScope = "OtherTransaction/Custom/ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF.WCFClient/GetData"},
                new Assertions.ExpectedMetric(){ callCount = _countClientInvocationMethodsToTest * 2, metricName = $"ExternalTransaction/{serverName}/{CATCrossProcessID_Service}/WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.SyncThrowException"},
                new Assertions.ExpectedMetric(){ callCount = _countClientInvocationMethodsToTest * 2, metricName = $"ExternalTransaction/{serverName}/{CATCrossProcessID_Service}/WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.SyncThrowException",
                    metricScope = "OtherTransaction/Custom/ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF.WCFClient/ThrowException"},

                new Assertions.ExpectedMetric(){ metricName = "Supportability/CrossApplicationTracing/Request/Create/Success" , callCount = countExpectedCreate },//16
				new Assertions.ExpectedMetric(){ metricName = "Supportability/CrossApplicationTracing/Request/Accept/Success", callCount = countExpectedAccept}, //24
				new Assertions.ExpectedMetric(){ metricName = "Supportability/CrossApplicationTracing/Response/Create/Success", callCount = countExpectedResponse },
                new Assertions.ExpectedMetric(){ metricName = "Supportability/CrossApplicationTracing/Response/Accept/Success", callCount = _countClientInvocationMethodsToTest * COUNT_SVC_METHODS },
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric() { metricName = "Supportability/CrossApplicationTracing/Request/Create/Exception" },
                new Assertions.ExpectedMetric() { metricName = "Supportability/CrossApplicationTracing/Request/Create/Exception/CrossProcessID" },
                new Assertions.ExpectedMetric() { metricName = "Supportability/CrossApplicationTracing/Request/Accept/Exception" },
                new Assertions.ExpectedMetric() { metricName = "Supportability/CrossApplicationTracing/Request/Accept/Warning/MultipleAttempts" },

                new Assertions.ExpectedMetric() { metricName = "Supportability/CrossApplicationTracing/Response/Create/Exception" },

                new Assertions.ExpectedMetric() { metricName = "Supportability/CrossApplicationTracing/Response/Accept/Exception" },
                new Assertions.ExpectedMetric() { metricName = "Supportability/CrossApplicationTracing/Response/Accept/Ignored/MultipleAttempts" }
            };

            //There is the potential that TAP call continuation will not complete prior to the transaction
            //being closed.  When this occurs, we cannot rely on the counts as the CAT info will not be applied
            //to the transaction.
            if (_thereWereCATFailures)
            {
                expectedMetrics.ForEach(x => x.callCount = null);
            }

            if (_tracingTestOption == TracingTestOption.CAT)
            {
                NrAssert.Multiple(
                    () => Assertions.MetricsExist(expectedMetrics, LogHelpers.MetricValues),
                    () => Assertions.MetricsDoNotExist(unexpectedMetrics, LogHelpers.MetricValues)
                );
            }
            else
            {
                NrAssert.Multiple(
                    () => Assertions.MetricsDoNotExist(expectedMetrics, LogHelpers.MetricValues),
                    () => Assertions.MetricsDoNotExist(unexpectedMetrics, LogHelpers.MetricValues)
                );
            }
        }

        [Fact]
        public override void DistributedTracing_SpanEvents()
        {

            if (_tracingTestOption != TracingTestOption.DT)
            {
                return;
            }

            var countSampledTrxs = LogHelpers.TrxEvents_Client.Count(x => (bool)x.IntrinsicAttributes["sampled"]);
            var countExpectedSpans = countSampledTrxs * 3;  //(root span, external call, service method)
            Assert.True(countExpectedSpans == LogHelpers.SpanEvents_Client.Length, $"Incorrect Number of Spans, Expected {countExpectedSpans}, Actual {LogHelpers.SpanEvents_Client.Length}");

            //Http-based endpoints should get have the http.statusCode attribute set on them
            var externalHttpSpanEvents = _fixture.AgentLog.GetSpanEvents()
                .Where(e => e.AgentAttributes.ContainsKey("http.url") && new Uri((string)e.AgentAttributes["http.url"]).Scheme.ToLower().StartsWith("http"))
                .ToList();
            Assert.All(externalHttpSpanEvents, e => Assert.Contains("http.statusCode", e.AgentAttributes.Keys));
        }

        [Fact]
        public void BindingType_Metrics()
        {
            var bindingName = $"{_bindingToTest}Binding";
            var expectedMetrics = SystemBindingNames.Contains(bindingName)
                ? new List<Assertions.ExpectedMetric> { new Assertions.ExpectedMetric() { metricName = $"Supportability/WCFClient/BindingType/{bindingName}", callCount = 1 } }
                : new List<Assertions.ExpectedMetric> { new Assertions.ExpectedMetric() { metricName = $"Supportability/WCFClient/BindingType/CustomBinding", callCount = 1 } };

            var unexpectedMetrics = SystemBindingNames.Where(binding => binding != bindingName).Select(binding => new Assertions.ExpectedMetric() { metricName = $"Supportability/WCFClient/BindingType/{binding}" }).ToList();
            if (bindingName != "CustomBinding" && bindingName != "CustomClassBinding")
            {
                unexpectedMetrics.Add(new Assertions.ExpectedMetric() { metricName = $"Supportability/WCFClient/BindingType/CustomBinding" });
            }

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, LogHelpers.MetricValues),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, LogHelpers.MetricValues)
            );
        }
    }
}
#endif
