// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using NewRelic.Testing.Assertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;

namespace NewRelic.Agent.IntegrationTests.WCF.Service
{
    public abstract class WCFServiceTestBase : WCFLegacyTestBase
    {
        private static WCFInvocationMethod[] _instrumentedSvcInvocMethods = new[]
        {
            WCFInvocationMethod.Sync,
            WCFInvocationMethod.BeginEndAsync,
            WCFInvocationMethod.TAPAsync
        };

        public WCFServiceTestBase(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output, WCFBindingType bindingToTest, TracingTestOption tracingTestOption, HostingModel hostingModelOption, ASPCompatibilityMode aspCompatModeOption, IWCFLogHelpers logHelpersImpl)
            : base(fixture, output, bindingToTest, new[] { WCFInvocationMethod.Sync }, _instrumentedSvcInvocMethods, tracingTestOption, hostingModelOption, aspCompatModeOption, logHelpersImpl)
        {
        }

        protected override int _expectedTransactionCount_Client => _countServiceInvocationMethodsToTest * COUNT_SVC_METHODS;    //2 methods being called (getdata, throwException)
        protected override int _expectedTransactionCount_Service => _countServiceInvocationMethodsToTest * COUNT_SVC_METHODS;   //2 methods being called (getdata, throwException)

        public static readonly string TEST_ERROR_MESSAGE = "WCF Service Testing Exception";

        [Fact]
        public override void TransactionEvents()
        {
            var expectedUserAttrib = new Dictionary<string, object>
            {
                {"custom key","custom value" },
                {"custom foo","custom bar" }
            };

            var expectedErrorTrxCount = _countServiceInvocationMethodsToTest * 2;       //2-error types

            //Transaction Count
            Assert.True(_expectedTransactionCount_Service == LogHelpers.TrxEvents_Service.Length, $"Service Transaction Count - Expected {_expectedTransactionCount_Service}, Actual {LogHelpers.TrxEvents_Service.Length}");


            var expectedTrxCount = _expectedTransactionCount;

            //When hosted in IIS with ASPCompatibility turned on, each client connection
            //results in 3 extra transactions (presumably negotiating security).
            if (_bindingToTest == WCFBindingType.WSHttp && _hostingModelOption == HostingModel.IIS && _aspCompatibilityOption == ASPCompatibilityMode.Enabled)
            {
                //Test Pattern:
                //	Connect					+3 transactions
                //	GetData()
                //	ThrowException(true)	
                //	Connect					+3 transactions
                //	ThrowException(false)
                expectedTrxCount += _countServiceInvocationMethodsToTest * 3 * 2;
            }

            //We shouldn't have any extra transactions
            Assert.True(expectedTrxCount == LogHelpers.TrxEvents.Length, $"Total Transaction Count - Expected {expectedTrxCount}, Actual {LogHelpers.TrxEvents.Length}");

            foreach (var svcTrx in LogHelpers.TrxEvents_Service)
            {
                //Check Custom Parameters
                Assertions.TransactionEventHasAttributes(expectedUserAttrib, TransactionEventAttributeType.User, svcTrx);

                //Check URI.  It differs depending on the binding (WebHTTP)
                var uri = svcTrx.AgentAttributes["request.uri"] as string;

                switch (_bindingToTest)
                {
                    case WCFBindingType.WebHttp:
                        Assert.True(uri.EndsWith("GetData") || uri.EndsWith("ThrowException") || uri.EndsWith("ThrowExceptionAtStart") || uri.EndsWith("ThrowExceptionAtEnd"), $"expected request.uri to be *GetData or *ThrowException, actual '{uri}'");
                        break;

                    case WCFBindingType.BasicHttp:
                    case WCFBindingType.WSHttp:
                    case WCFBindingType.NetTcp:
                    default:

                        var expectedUri = _hostingModelOption == HostingModel.Self
                            ? $"/{_relativePath}"
                            : $"/{IIS_SERVICENAME}/{_relativePath}";

                        Assert.True(uri == expectedUri, $"expected request.uri to be '{expectedUri}', actual '{uri}'");
                        break;
                }
            }

            //Error - Counts
            var countTrxErrorAttrib = LogHelpers.TrxEvents_Service
                .SelectMany(trx => trx.IntrinsicAttributes
                .Where(x => x.Key == "error" && (bool)x.Value))
                .Count();

            var countTrxErrMsg = LogHelpers.TrxEvents_Service
                .SelectMany(trx => trx.IntrinsicAttributes
                .Where(x => x.Key == "errorMessage" && x.Value.ToString() == TEST_ERROR_MESSAGE))
                .Count();

            Assert.True(expectedErrorTrxCount == countTrxErrorAttrib, $"Expected {expectedErrorTrxCount} Service Transactions with Errors, (Actual {countTrxErrorAttrib})");
            Assert.True(expectedErrorTrxCount == countTrxErrMsg, $"Expected {expectedErrorTrxCount} Service Transactions with expected error message, (Actual {countTrxErrorAttrib})");


            //Test Transaction Names match our service methods
            var countTrxGetData = LogHelpers.TrxEvents_Service.Count(x => x.IntrinsicAttributes["name"].ToString().EndsWith("GetData"));
            var countTrxThrowExp = LogHelpers.TrxEvents_Service.Count(x =>
            {
                var trxName = x.IntrinsicAttributes["name"].ToString();
                return trxName.EndsWith("ThrowExceptionAtStart") || trxName.EndsWith("ThrowExceptionAtEnd") || trxName.EndsWith("ThrowException");
            });

            Assert.True(_countServiceInvocationMethodsToTest == countTrxGetData, $"Expected {_countServiceInvocationMethodsToTest} Service Transactions - GetData, (Actual {countTrxGetData})");
            Assert.True(expectedErrorTrxCount == countTrxThrowExp, $"Expected {expectedErrorTrxCount} Service Transactions - ThrowException, (Actual {countTrxThrowExp})");
        }

        [Fact]
        public override void Metrics()
        {

            var expectedTrxCount = _expectedTransactionCount_Service;

            //When hosted in IIS with ASPCompatibility turned on, each client connection
            //results in 3 extra transactions (presumably negotiating security).
            if (_bindingToTest == WCFBindingType.WSHttp && _hostingModelOption == HostingModel.IIS && _aspCompatibilityOption == ASPCompatibilityMode.Enabled)
            {
                //Test Pattern:
                //	Connect					+3 transactions
                //	GetData()
                //	ThrowException(true)	
                //	Connect					+3 transactions
                //	ThrowException(false)
                expectedTrxCount += _countServiceInvocationMethodsToTest * 3 * 2;
            }

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric(){ callCount = expectedTrxCount, metricName = "WebTransaction" },
                new Assertions.ExpectedMetric(){ callCount = _countServiceInvocationMethodsToTest * 2, metricName="Supportability/Events/TransactionError/Seen"},

                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.SyncGetData" },
                new Assertions.ExpectedMetric(){ callCount =2, metricName = $"WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.SyncThrowException" },

                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"DotNet/{SharedWcfLibraryNamespace}.IWcfService.SyncGetData" },
                new Assertions.ExpectedMetric(){ callCount =2, metricName = $"DotNet/{SharedWcfLibraryNamespace}.IWcfService.SyncThrowException" },

                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.BeginAsyncGetData" },
                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.BeginAsyncThrowExceptionAtStart" },
                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.BeginAsyncThrowExceptionAtEnd" },

                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"DotNet/{SharedWcfLibraryNamespace}.IWcfService.BeginAsyncGetData" },
                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"DotNet/{SharedWcfLibraryNamespace}.IWcfService.EndAsyncGetData" },

                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"DotNet/{SharedWcfLibraryNamespace}.IWcfService.BeginAsyncThrowExceptionAtStart" },
                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"DotNet/{SharedWcfLibraryNamespace}.IWcfService.BeginAsyncThrowExceptionAtEnd" },
                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"DotNet/{SharedWcfLibraryNamespace}.IWcfService.EndAsyncThrowExceptionAtEnd" },

                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.TAPGetData" },
                new Assertions.ExpectedMetric(){ callCount =2, metricName = $"WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.TAPThrowException" },

                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"DotNet/{SharedWcfLibraryNamespace}.IWcfService.TAPGetData" },
                new Assertions.ExpectedMetric(){ callCount =2, metricName = $"DotNet/{SharedWcfLibraryNamespace}.IWcfService.TAPThrowException" },

                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"DotNet/{SharedWcfLibraryNamespace}.IWcfService.TAPGetData", metricScope = $"WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.TAPGetData" },
                new Assertions.ExpectedMetric(){ callCount =2, metricName = $"DotNet/{SharedWcfLibraryNamespace}.IWcfService.TAPThrowException", metricScope = $"WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.TAPThrowException"  },

                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"DotNet/{SharedWcfLibraryNamespace}.IWcfService.SyncGetData", metricScope = $"WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.SyncGetData" },
                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"DotNet/{SharedWcfLibraryNamespace}.IWcfService.BeginAsyncGetData", metricScope = $"WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.BeginAsyncGetData" },
                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"DotNet/{SharedWcfLibraryNamespace}.IWcfService.EndAsyncGetData", metricScope = $"WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.BeginAsyncGetData" },

                new Assertions.ExpectedMetric(){ callCount =2, metricName = $"DotNet/{SharedWcfLibraryNamespace}.IWcfService.SyncThrowException", metricScope = $"WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.SyncThrowException" },
                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"DotNet/{SharedWcfLibraryNamespace}.IWcfService.BeginAsyncThrowExceptionAtStart", metricScope = $"WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.BeginAsyncThrowExceptionAtStart" },

                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"DotNet/{SharedWcfLibraryNamespace}.IWcfService.BeginAsyncThrowExceptionAtEnd", metricScope = $"WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.BeginAsyncThrowExceptionAtEnd" },
                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"DotNet/{SharedWcfLibraryNamespace}.IWcfService.EndAsyncThrowExceptionAtEnd", metricScope = $"WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.BeginAsyncThrowExceptionAtEnd" },

                new Assertions.ExpectedMetric(){ callCount =1, metricName = "External/www.google.com/Stream/GET", metricScope = $"WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.BeginAsyncGetData" },
                new Assertions.ExpectedMetric(){ callCount =1, metricName = "External/www.google.com/Stream/GET", metricScope = $"WebTransaction/WCF/{SharedWcfLibraryNamespace}.IWcfService.SyncGetData" }
            };

            Assertions.MetricsExist(expectedMetrics, LogHelpers.MetricValues);
        }

        [Fact]
        public override void DistributedTracing_Metrics()
        {
            if (_tracingTestOption != TracingTestOption.DT)
            {
                return;
            }

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric(){ metricName = $"Supportability/DistributedTrace/AcceptPayload/Ignored" },
                new Assertions.ExpectedMetric() { metricName = $"Supportability/DistributedTrace/AcceptPayload/Ignored/Multiple" },
                new Assertions.ExpectedMetric(){ metricName = $"Supportability/DistributedTrace/AcceptPayload/Ignored/CreateBeforeAccept" },
                new Assertions.ExpectedMetric(){ metricName = $"Supportability/DistributedTrace/AcceptPayload/Ignored/MajorVersion" },
                new Assertions.ExpectedMetric(){ metricName = $"Supportability/DistributedTrace/AcceptPayload/Ignored/Null" },
                new Assertions.ExpectedMetric(){ metricName = $"Supportability/DistributedTrace/AcceptPayload/Ignored/UntrustedAccount" },
                new Assertions.ExpectedMetric(){ metricName = $"Supportability/DistributedTrace/AcceptPayload/Exception" },
                new Assertions.ExpectedMetric(){ metricName = $"Supportability/DistributedTrace/AcceptPayload/ParseException" },
                new Assertions.ExpectedMetric(){ metricName = $"Supportability/DistributedTrace/CreatePayload/Exception" },

                new Assertions.ExpectedMetric(){ metricName = $"Supportability/TraceContext/Accept/Exception" },
                new Assertions.ExpectedMetric(){ metricName = $"Supportability/TraceContext/Create/Exception" },
                new Assertions.ExpectedMetric(){ metricName = $"Supportability/TraceContext/TraceParent/Parse/Exception" },
                new Assertions.ExpectedMetric(){ metricName = $"Supportability/TraceContext/TraceState/Parse/Exception" },
                new Assertions.ExpectedMetric(){ metricName = $"Supportability/TraceContext/TraceState/InvalidNrEntry" },
                new Assertions.ExpectedMetric(){ metricName = $"Supportability/TraceContext/TraceState/NoNrEntry" },
            };

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric(){ callCount = _countServiceInvocationMethodsToTest * COUNT_SVC_METHODS, metricName = $"DurationByCaller/App/{AccountID_Client}/{AppID_Client}/{ExpectedTransportType}/all" },
                new Assertions.ExpectedMetric(){ callCount = _countServiceInvocationMethodsToTest * COUNT_SVC_METHODS, metricName = $"DurationByCaller/App/{AccountID_Client}/{AppID_Client}/{ExpectedTransportType}/allWeb" },
                new Assertions.ExpectedMetric(){ callCount = _countServiceInvocationMethodsToTest * COUNT_SVC_METHODS, metricName = $"TransportDuration/App/{AccountID_Client}/{AppID_Client}/{ExpectedTransportType}/all" },
                new Assertions.ExpectedMetric(){ callCount = _countServiceInvocationMethodsToTest * COUNT_SVC_METHODS, metricName = $"TransportDuration/App/{AccountID_Client}/{AppID_Client}/{ExpectedTransportType}/allWeb" },

                new Assertions.ExpectedMetric(){ metricName = "Supportability/DistributedTrace/CreatePayload/Success",
                    callCount =     (_countServiceInvocationMethodsToTest * COUNT_SVC_METHODS)		//Each Client Call
								+   _countServiceInvocationMethodsToTest							//Covering the HTTP call in GetData
								+   1																//Covering the HTTP call in AsyncThrowExceptionAtEnd (it call DoWork which does an HTTP Request)
				},

                new Assertions.ExpectedMetric(){ metricName = "Supportability/TraceContext/Create/Success",
                    callCount =     (_countServiceInvocationMethodsToTest * COUNT_SVC_METHODS)		//Each Client Call
								+   _countServiceInvocationMethodsToTest							//Covering the HTTP call in GetData
								+   1																//Covering the HTTP call in AsyncThrowExceptionAtEnd (it call DoWork which does an HTTP Request)
				},
				//In IIS Hosted Scenarios with ASPCompatibility Disabled, the ignore transaction wrapper is used on the ASP.Pipeline hit.
				//However, before this occurs, an accept has occurred, which doubles the number of accepts.
				new Assertions.ExpectedMetric(){ metricName = "Supportability/TraceContext/Accept/Success",
                        callCount = _hostingModelOption == HostingModel.Self || _aspCompatibilityOption == ASPCompatibilityMode.Enabled
                            ? _countServiceInvocationMethodsToTest * COUNT_SVC_METHODS
                            : _countServiceInvocationMethodsToTest * COUNT_SVC_METHODS * 2
                },
            };

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, LogHelpers.MetricValues),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, LogHelpers.MetricValues)
            );
        }

        [Fact]
        public override void CAT_Metrics()
        {
            var countExpectedAccept = _hostingModelOption == HostingModel.Self || _aspCompatibilityOption == ASPCompatibilityMode.Enabled
                ? _countServiceInvocationMethodsToTest * COUNT_SVC_METHODS
                : _countServiceInvocationMethodsToTest * COUNT_SVC_METHODS * 2;     //second CAT Accept call for the ignored transaction


            var countExpectedResponse = _countServiceInvocationMethodsToTest * COUNT_SVC_METHODS;

            var countExpectedCreate = _countServiceInvocationMethodsToTest * COUNT_SVC_METHODS      //1 for each WCF Client call
                                    + _countServiceInvocationMethodsToTest                          //1 for GetData's HTTP call on the service
                                    + 1;                                                            //1 for the DoWork call in the AsyncThrowExceptionAtEnd from
                                                                                                    //testing WCFInvocationMethod.BeginEndAsync 


            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric(){ callCount = _countServiceInvocationMethodsToTest * COUNT_SVC_METHODS, metricName = $"ClientApplication/{CATCrossProcessID_Client}/all" },

                new Assertions.ExpectedMetric(){ metricName = "Supportability/CrossApplicationTracing/Request/Create/Success" , callCount = countExpectedCreate },
                new Assertions.ExpectedMetric(){ metricName = "Supportability/CrossApplicationTracing/Request/Accept/Success", callCount = countExpectedAccept},
                new Assertions.ExpectedMetric(){ metricName = "Supportability/CrossApplicationTracing/Response/Create/Success", callCount = countExpectedResponse },
                new Assertions.ExpectedMetric(){ metricName = "Supportability/CrossApplicationTracing/Response/Accept/Success", callCount = _countServiceInvocationMethodsToTest * COUNT_SVC_METHODS },
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric() { metricName = "Supportability/CrossApplicationTracing/Request/Create/Exception" },
                new Assertions.ExpectedMetric() { metricName = "Supportability/CrossApplicationTracing/Request/Create/Exception/CrossProcessID" },
                new Assertions.ExpectedMetric() { metricName = "Supportability/CrossApplicationTracing/Request/Accept/Exception" },
                new Assertions.ExpectedMetric() { metricName = "Supportability/CrossApplicationTracing/Request/Accept/Warning/MultipleAttempts" },

                new Assertions.ExpectedMetric() { metricName = "Supportability/CrossApplicationTracing/Response/Create/Exception" },
                new Assertions.ExpectedMetric() { metricName = "Supportability/CrossApplicationTracing/Response/Create/Exception" },
                new Assertions.ExpectedMetric() { metricName = "Supportability/CrossApplicationTracing/Response/Accept/Exception" },
                new Assertions.ExpectedMetric() { metricName = "Supportability/CrossApplicationTracing/Response/Accept/Ignored/MultipleAttempts" }
            };



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
        public void ErrorEvents()
        {
            var errorEvents = LogHelpers.ErrorEvents;

            Assert.True(errorEvents.Any(), "Unable to test ErrorEvents because no error event information is not available in the log.");

            var errorEventTrxNames = errorEvents.SelectMany(e => e.IntrinsicAttributes
                .Where(a => a.Key == "transactionName")
                .Select(a => a.Value.ToString()))
                .ToArray();

            var unexpectedErrorTransactions = errorEventTrxNames
                .Where(x => !x.Contains("WcfService") || (!x.EndsWith("ThrowException") && !x.EndsWith("ThrowExceptionAtStart") && !x.EndsWith("ThrowExceptionAtEnd")))
                .ToArray();

            var countErrorEventsErrMsg = errorEvents.SelectMany(e => e.IntrinsicAttributes
                .Where(a => a.Key == "error.message" && a.Value.ToString() == TEST_ERROR_MESSAGE))
                .Count();


            var countExpectedErrorEvents = _countServiceInvocationMethodsToTest * 2;

            NrAssert.Multiple(

                //We should have 1 eror event for each service side invocation called.  There shouldn't be any errors on the client side.
                () => Assert.True(errorEvents.Length == countExpectedErrorEvents, $"Expected {countExpectedErrorEvents} Error Events, Actual {errorEvents.Length}"),

                //All of our error events should be on the service side and should be for an explicit method that ThrowsAnException
                () => Assert.True(unexpectedErrorTransactions.Length == 0, $"The following transactions have errors, but were not expected to: {string.Join(", ", errorEventTrxNames)}"),

                //The error message should be correct.
                () => Assert.True(countExpectedErrorEvents == countErrorEventsErrMsg, $"Expected {countExpectedErrorEvents} Error Events with '{TEST_ERROR_MESSAGE}' message.  Actual {countErrorEventsErrMsg}")
            );
        }

        [Fact]
        public override void DistributedTracing_SpanEvents()
        {
            if (_tracingTestOption != TracingTestOption.DT)
            {
                return;
            }

            //Iterate through the transactions that were sampled.  These transaction should result in span-events.
            //The number of events depends on the specific service method called.
            var svcTrx = LogHelpers.TrxEvents_Service;
            var svcTrxSampled = svcTrx.Where(x => (bool)x.IntrinsicAttributes["sampled"]).ToArray();
            var svcTrxNames = svcTrxSampled.Select(x => x.IntrinsicAttributes["name"].ToString()).ToList();

            var countExpectedSpans = 0;
            var unexpectedMethodNames = new List<string>();

            var includeASPPipelineEvents = _hostingModelOption == HostingModel.IIS && _aspCompatibilityOption == ASPCompatibilityMode.Enabled;

            // In debug we consistently get 10 extra ASP pipeline events, in Release we consistently get 9.
            // We do not see spans for 'PreExecuteRequestHandler' in Release, but we do in Debug.
            #if DEBUG
                const int COUNT_ASP_PIPELINE_EVENTS = 10;
            #else
                const int COUNT_ASP_PIPELINE_EVENTS = 9;
            #endif

            foreach (var svcTrxName in svcTrxNames)
            {
                countExpectedSpans++;       //Root Span

                var methodName = svcTrxName.Split('.').Last();
                switch (methodName)
                {
                    case "BeginAsyncGetData":
                        countExpectedSpans++;       //Begin
                        countExpectedSpans += includeASPPipelineEvents ? COUNT_ASP_PIPELINE_EVENTS : 0;
                        countExpectedSpans++;       //HTTP Request to Google
                        countExpectedSpans++;       //End

                        break;

                    case "SyncThrowException":
                        countExpectedSpans++;
                        countExpectedSpans += includeASPPipelineEvents ? COUNT_ASP_PIPELINE_EVENTS : 0;
                        break;

                    case "BeginAsyncThrowExceptionAtEnd":
                        countExpectedSpans++;       //Begin
                        countExpectedSpans += includeASPPipelineEvents ? COUNT_ASP_PIPELINE_EVENTS : 0;
                        countExpectedSpans++;       //HTTP Request
                        countExpectedSpans++;       //End
                        break;

                    case "BeginAsyncThrowExceptionAtStart":
                        countExpectedSpans++;       //Begin
                        countExpectedSpans += includeASPPipelineEvents ? COUNT_ASP_PIPELINE_EVENTS : 0;
                        break;

                    case "SyncGetData":
                        countExpectedSpans++;       //Service Call
                        countExpectedSpans += includeASPPipelineEvents ? COUNT_ASP_PIPELINE_EVENTS : 0;
                        countExpectedSpans++;       //HTTP Request to Google
                        break;

                    case "TAPGetData":
                        countExpectedSpans++;       //InvokeBegin
                        countExpectedSpans += includeASPPipelineEvents ? COUNT_ASP_PIPELINE_EVENTS : 0;
                        countExpectedSpans++;       //HTTP Request
                        break;

                    case "TAPThrowException":
                        countExpectedSpans++;       //InvokeBegin
                        countExpectedSpans += includeASPPipelineEvents ? COUNT_ASP_PIPELINE_EVENTS : 0;
                        break;

                    default:
                        unexpectedMethodNames.Add(methodName);
                        break;
                }
            }

            // Leaving this here commented out to help future code warriors who may want to dump and compare spans between runs
            //using (var tempFileStream = new FileStream($"C:\\{Guid.NewGuid()}.spandump", FileMode.CreateNew))
            //using (var sr = new StreamWriter(tempFileStream))
            //{
            //    foreach (var spanEvent in LogHelpers.SpanEvents_Service.OrderBy(x => x.IntrinsicAttributes["name"]))
            //    {
            //        sr.WriteLine($"Span event name: {spanEvent.IntrinsicAttributes["name"]}");
            //    }
            //}

            NrAssert.Multiple(
                () => Assert.True(countExpectedSpans == LogHelpers.SpanEvents_Service.Length, $"Incorrect Number of Spans, Expected {countExpectedSpans}, Actual {LogHelpers.SpanEvents_Service.Length}"),
                () => Assert.True(!unexpectedMethodNames.Any(), $"The following methods were not Recognized {string.Join(", ", unexpectedMethodNames.ToArray())}")
            );
        }

        [Fact]
        public void ErrorTraces()
        {
            var countExpectedErrorEvents = _countServiceInvocationMethodsToTest * 2;


            //We should have 1 error trace for each invocation method (ThrowException call)
            NrAssert.Multiple(
                () => Assert.True(countExpectedErrorEvents == LogHelpers.ErrorTraces.Length, $"Expected {countExpectedErrorEvents} Error Traces, Actual {LogHelpers.ErrorTraces.Length}."),
                () => Assert.True(LogHelpers.ErrorTraces.All(e => e.Message == TEST_ERROR_MESSAGE), $"Some error traces do not have message '{TEST_ERROR_MESSAGE}'"),
                () => Assert.True(LogHelpers.ErrorTraces.All(e => e.Path.EndsWith("ThrowException") || e.Path.EndsWith("ThrowExceptionAtStart") || e.Path.EndsWith("ThrowExceptionAtEnd")), "Some error traces do not have path ending with 'ThrowException'"),
                () => Assert.True(LogHelpers.ErrorTraces.All(e => e.Attributes.StackTrace != null && e.Attributes.StackTrace.Count() > 0), "Some error traces do not have a stack trace")
            );
        }

        [Fact]
        public void BindingType_Metrics()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>();
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>();
            var bindingName = $"{_bindingToTest}Binding";
            if (SystemBindingNames.Contains(bindingName))
            {
                expectedMetrics.Add(new Assertions.ExpectedMetric() { metricName = $"Supportability/WCFService/BindingType/{bindingName}", callCount = 1 });
            }
            else
            {
                expectedMetrics.Add(new Assertions.ExpectedMetric() { metricName = $"Supportability/WCFService/BindingType/CustomBinding", callCount = 1 });

                if (_bindingToTest != WCFBindingType.Custom)
                {
                    unexpectedMetrics.Add(new Assertions.ExpectedMetric() { metricName = $"Supportability/WCFService/BindingType/{_bindingToTest}Binding" });
                }
            }

            var assertions = new List<Action>();
            assertions.Add(() => Assertions.MetricsExist(expectedMetrics, LogHelpers.MetricValues));
            assertions.Add(() => Assertions.MetricsDoNotExist(unexpectedMetrics, LogHelpers.MetricValues));
            NrAssert.Multiple(assertions.ToArray());
        }

    }
}
#endif
