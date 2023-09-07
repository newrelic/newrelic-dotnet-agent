// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NETFRAMEWORK
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.WCF
{
    public abstract class WCFLegacyTestBase : NewRelicIntegrationTest<ConsoleDynamicMethodFixtureFWLatest>
    {
        public enum TracingTestOption
        {
            CAT,
            DT,
            None
        }

        public enum HostingModel
        {
            Self,
            IIS,
            IISNoAsp
        }

        public enum ASPCompatibilityMode
        {
            Enabled,
            Disabled
        }

        public const int COUNT_SVC_METHODS = 3;      //GetData, ThrowException(true), ThrowException(false)

        public static readonly WCFBindingType[] WCFBindingTypes_All = Enum.GetValues(typeof(WCFBindingType)).Cast<WCFBindingType>().ToArray();
        public static readonly WCFInvocationMethod[] WCFInvocationMethods_All = Enum.GetValues(typeof(WCFInvocationMethod)).Cast<WCFInvocationMethod>().ToArray();

        protected readonly ConsoleDynamicMethodFixtureFWLatest _fixture;

        protected readonly WCFBindingType _bindingToTest;
        protected string ExpectedTransportType => _bindingToTest == WCFBindingType.NetTcp
            ? "Other"
            : "HTTP";

        protected readonly WCFInvocationMethod[] _serviceInvocationMethodsToTest;
        protected int _countServiceInvocationMethodsToTest => _serviceInvocationMethodsToTest.Length;

        protected readonly WCFInvocationMethod[] _clientInvocationMethodsToTest;
        protected int _countClientInvocationMethodsToTest => _clientInvocationMethodsToTest.Length;

        protected readonly TracingTestOption _tracingTestOption;
        protected readonly HostingModel _hostingModelOption;
        protected readonly ASPCompatibilityMode _aspCompatibilityOption;

        protected readonly string _relativePath;

        protected const string IIS_SERVICENAME = "WcfService.svc";
        protected const string SharedWcfLibraryNamespace = "NewRelic.Agent.IntegrationTests.Shared.Wcf";


        protected string IISWebAppPublishPath => Path.Combine(_fixture.IntegrationTestAppPath, "WcfAppIisHosted", "Deploy");

        public static readonly List<string> SystemBindingNames = new List<string>
            {
                "BasicHttpBinding",
                "WSHttpBinding",
                "WSDualHttpBinding",
                "WSFederationHttpBinding",
                "NetHttpBinding",
                "NetHttpsBinding",
                "NetTcpBinding",
                "NetNamedPipeBinding",
                "NetMsmqBinding",
                "NetPeerTcpBinding",
                "MsmqIntegrationBinding",
                "BasicHttpContextBinding",
                "NetTcpContextBinding",
                "WebHttpBinding",
                "WSHttpContextBinding",
                "UdpBinding"
            };

        protected static readonly Dictionary<WCFInvocationMethod, string> SupMetricNames_Service_InvocationType = new Dictionary<WCFInvocationMethod, string>()
        {
            {WCFInvocationMethod.Sync, "Supportability/WCFService/InvocationStyle/Sync" },
            {WCFInvocationMethod.BeginEndAsync, "Supportability/WCFService/InvocationStyle/APM" },
            {WCFInvocationMethod.TAPAsync, "Supportability/WCFService/InvocationStyle/TAP" },
        };

        protected static readonly Dictionary<WCFInvocationMethod, string> SupMetricNames_Client_InvocationType = new Dictionary<WCFInvocationMethod, string>()
        {
            {WCFInvocationMethod.Sync, "Supportability/WCFClient/InvocationStyle/Sync" },
            {WCFInvocationMethod.BeginEndAsync, "Supportability/WCFClient/InvocationStyle/APM" },
            {WCFInvocationMethod.EventBasedAsync, "Supportability/WCFClient/InvocationStyle/APM" },
            {WCFInvocationMethod.TAPAsync, "Supportability/WCFClient/InvocationStyle/TAP" },
        };

        public WCFLegacyTestBase(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output,
            WCFBindingType bindingToTest,
            IEnumerable<WCFInvocationMethod> clientInvocationsToTest,
            IEnumerable<WCFInvocationMethod> serviceInvocationsToTest,
            TracingTestOption tracingTestOption,
            HostingModel hostingModelOption,
            ASPCompatibilityMode aspCompatModeOption,
            IWCFLogHelpers logHelpersImpl
            ) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _bindingToTest = bindingToTest;
            _clientInvocationMethodsToTest = clientInvocationsToTest.ToArray();
            _serviceInvocationMethodsToTest = serviceInvocationsToTest.ToArray();
            _tracingTestOption = tracingTestOption;
            _hostingModelOption = hostingModelOption;
            _aspCompatibilityOption = aspCompatModeOption;

            _relativePath = $"Test_{_bindingToTest}";

            LogHelpers = logHelpersImpl;

            // AddActions() executes the applied actions after actions defined by the base.
            // In this case the base defines an exerciseApplication action we want to wait after.
            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    _fixture.RemoteApplication.NewRelicConfig.SetLogLevel("finest");
                    _fixture.RemoteApplication.NewRelicConfig.SetRequestTimeout(TimeSpan.FromSeconds(10));
                    _fixture.RemoteApplication.NewRelicConfig.ForceTransactionTraces();
                    _fixture.RemoteApplication.NewRelicConfig.EnableSpanEvents(true);
                    _fixture.RemoteApplication.NewRelicConfig.SetOrDeleteDistributedTraceEnabled(_tracingTestOption == TracingTestOption.DT ? true : null as bool?);
                    _fixture.RemoteApplication.NewRelicConfig.SetCATEnabled(_tracingTestOption == TracingTestOption.CAT);

                    GenerateFixtureCommands();

                    _fixture.SetTimeout(TimeSpan.FromMinutes(5));

                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );


            _fixture.Initialize();
        }


        /// <summary>
        /// Generates the console app commands to run based on the requested test pattern
        /// </summary>
        private void GenerateFixtureCommands()
        {

            switch (_hostingModelOption)
            {
                case HostingModel.Self:
                    _fixture.AddCommand($"WCFServiceSelfHosted StartService {_bindingToTest} {_fixture.RemoteApplication.Port} {_relativePath}");
                    break;
                case HostingModel.IIS:
                    _fixture.AddCommand($"WCFServiceIISHosted StartService {IISWebAppPublishPath} {_bindingToTest} {_fixture.RemoteApplication.Port} {_relativePath} {(_aspCompatibilityOption == ASPCompatibilityMode.Enabled).ToString().ToLower()}");
                    break;
            }

            foreach (var clientInvocationMethod in this._clientInvocationMethodsToTest)
            {
                foreach (var serviceInvocationMethodObj in _serviceInvocationMethodsToTest)
                {

                    var serviceInvocationMethod = (WCFInvocationMethod)serviceInvocationMethodObj;

                    _fixture.AddCommand(GetInitializeClientFixtureCommand());

                    _fixture.AddCommand($"WCFClient GetData {clientInvocationMethod} {serviceInvocationMethod} 32");
                    _fixture.AddCommand($"WCFClient ThrowException {clientInvocationMethod} {serviceInvocationMethod} true");
                    _fixture.AddCommand(GetInitializeClientFixtureCommand());
                    _fixture.AddCommand($"WCFClient ThrowException {clientInvocationMethod} {serviceInvocationMethod} false");
                }
            }

            switch (_hostingModelOption)
            {
                case HostingModel.Self:
                    _fixture.AddCommand("WCFServiceSelfHosted StopService");
                    break;
                case HostingModel.IIS:
                    _fixture.AddCommand("WCFServiceIISHosted StopService");
                    break;
            }
        }

        private string GetInitializeClientFixtureCommand()
        {
            switch (_hostingModelOption)
            {
                case HostingModel.Self:
                    return $"WCFClient InitializeClient_SelfHosted {_bindingToTest} {_fixture.RemoteApplication.Port} {_relativePath}";
                case HostingModel.IIS:
                    return $"WCFClient InitializeClient_IISHosted {_bindingToTest} {_fixture.RemoteApplication.Port} {_relativePath}";
                default:
                    throw new Exception($"Hosting Option {_hostingModelOption} is not supported");
            }
        }


        //Helper Functions to obtain data from fixtures
        protected readonly IWCFLogHelpers LogHelpers;

        #region Helper Functions to make assertions on the data

        public string AppID_Client => LogHelpers.ConnectResponse_Client?.ApplicationId;
        public string AppID_Service => LogHelpers.ConnectResponse_Service?.ApplicationId;

        public string AccountID_Client => LogHelpers.ConnectResponse_Client?.AccountId;
        public string AccountID_Service => LogHelpers.ConnectResponse_Service?.AccountId;


        public string CATCrossProcessID_Client => LogHelpers.ConnectResponse_Client?.CrossProcessId;
        public string CATCrossProcessID_Service => LogHelpers.ConnectResponse_Service?.CrossProcessId;


        /// <summary>
        /// The number of transactions we expect for the client or the service
        /// They should be the same number on both the client and the service
        /// </summary>
        protected abstract int _expectedTransactionCount_Client { get; }

        protected abstract int _expectedTransactionCount_Service { get; }

        protected int _expectedTransactionCount => _expectedTransactionCount_Client + _expectedTransactionCount_Service;

        #endregion


        [Fact]
        public void TransactionsProperlyEnded()
        {
            var improperlyClosedTrx = LogHelpers.QueryLog((x) => x.TryGetTransactionEndedByGarbageCollector()).ToList();
            Assert.False(improperlyClosedTrx.Any(), $"There are {improperlyClosedTrx.Count()} transactions that were closed during finalization.");

            improperlyClosedTrx = LogHelpers.QueryLog((x) => x.TryGetTransactionHasAlreadyCapturedResponseTime()).ToList();
            Assert.False(improperlyClosedTrx.Any(), $"There are {improperlyClosedTrx.Count()} transactions that were closed too early. This results incorrect response time capture.");

        }

        [Fact]
        public abstract void TransactionEvents();

        [Fact]
        public abstract void Metrics();

        [Fact]
        public void DistributedTracing()
        {
            if (_tracingTestOption != TracingTestOption.DT)
            {
                return;
            }

            NrAssert.Multiple(
                () => Assert.NotEmpty(LogHelpers.TrxEvents),
                () => Assert.NotEmpty(LogHelpers.TrxEvents_Client),
                () => Assert.NotEmpty(LogHelpers.TrxEvents_Service));

            //The expected Attributes on the Service Side Transaction
            var dtAttributes = new[]
            {
                "parentId",
                "parent.transportType",
                "guid",
                "traceId",
                "parentSpanId",
                "parent.type",
                "parent.app",
                "parent.account",
                "parent.transportDuration"
            };

            foreach (var svcTrx in LogHelpers.TrxEvents_Service)
            {
                //Assert that the transaction has the DT attributes
                Assertions.TransactionEventHasAttributes(dtAttributes, TransactionEventAttributeType.Intrinsic, svcTrx);

                // Verify the correct transport type 
                var actualTransportType = svcTrx.IntrinsicAttributes["parent.transportType"].ToString();
               
                Assert.True(actualTransportType == ExpectedTransportType, $"Mismatched TransportType, expected {ExpectedTransportType}, actual {actualTransportType}");

                //Given the ParentID on the Svc Transaction, we should find the client transaction that matches.
                var parentTrxID = svcTrx.IntrinsicAttributes["parentId"].ToString();
                Assert.Contains(parentTrxID, LogHelpers.TrxIDs_Client);

                //Given the traceId on the Svc Transaction, we should find the client transaction that matches.
                var svcTraceId = svcTrx.IntrinsicAttributes["traceId"].ToString();
                var clientWithMatchingTraceId = LogHelpers.TrxEvents_Client.
                    Where(tx => tx.IntrinsicAttributes["traceId"].ToString().Equals(svcTraceId));
                Assert.NotEmpty(clientWithMatchingTraceId);

                NrAssert.Multiple(
                    () => Assert.Equal(AccountID_Client, svcTrx.IntrinsicAttributes["parent.account"]),
                    () => Assert.Equal(AppID_Client, svcTrx.IntrinsicAttributes["parent.app"])
                );
            }
        }

        [Fact]
        public abstract void DistributedTracing_Metrics();

        [Fact]
        public abstract void DistributedTracing_SpanEvents();

        [Fact]
        public void SupportabilityMetrics_InvocationMethod_Service()
        {
            var expectedMetrics = _serviceInvocationMethodsToTest
                .Select(invocType => SupMetricNames_Service_InvocationType[invocType]).Distinct()
                .Select(metricNM => new Assertions.ExpectedMetric() { metricName = metricNM, callCount = 1 })
                .ToArray();

            var unexpectedMetrics = WCFInvocationMethods_All
                .Except(_serviceInvocationMethodsToTest)
                .Where(x => x != WCFInvocationMethod.EventBasedAsync)
                .Select(invocType => SupMetricNames_Service_InvocationType[invocType]).Distinct()
                .Select(metricNM => new Assertions.ExpectedMetric() { metricName = metricNM })
                .ToArray();

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, LogHelpers.MetricValues),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, LogHelpers.MetricValues)
            );
        }

        [Fact]
        public void SupportabilityMetrics_InvocationMethod_Client()
        {
            var expectedMetrics = _clientInvocationMethodsToTest
                .Select(invocType => SupMetricNames_Client_InvocationType[invocType]).Distinct()
                .Select(metricNM => new Assertions.ExpectedMetric() { metricName = metricNM, callCount = 1 })
                .ToArray();

            var unexpectedMetrics = WCFInvocationMethods_All
                .Except(_clientInvocationMethodsToTest)
                .Select(invocType => SupMetricNames_Client_InvocationType[invocType]).Distinct()
                .Select(metricNM => new Assertions.ExpectedMetric() { metricName = metricNM })
                .ToArray();

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, LogHelpers.MetricValues),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, LogHelpers.MetricValues)
            );
        }

        [Fact]
        public void CAT()
        {
            var assertions = new List<Action>();

            if (_tracingTestOption != TracingTestOption.CAT)
            {
                return;
            }

            //We do this assertion here so that we don't pass this test if transactions have not been generated
            NrAssert.Multiple(
                () => Assert.NotEmpty(LogHelpers.TrxEvents),
                () => Assert.NotEmpty(LogHelpers.TrxEvents_Client),
                () => Assert.NotEmpty(LogHelpers.TrxEvents_Service));

            var catAttributes = new[]
            {
                "nr.guid",
                "nr.tripId",
                "nr.pathHash",
                "nr.referringPathHash",
                "nr.referringTransactionGuid"
            };

            foreach (var trx in LogHelpers.TrxEvents_Service)
            {
                var svcTrx = trx;

                //Assert that the transaction has the DT attributes
                assertions.Add(() => Assertions.TransactionEventHasAttributes(catAttributes, TransactionEventAttributeType.Intrinsic, svcTrx));

                //Given the ParentID on the Svc Transcation, we should find the client transaction that matches.
                var referringTrxId = svcTrx.IntrinsicAttributes["nr.referringTransactionGuid"].ToString();

                assertions.Add(() => Assert.True(LogHelpers.TrxTripIDs_Client.Contains(referringTrxId), $"Service Transaction's referringTransactionGuid={referringTrxId}, could not find corresponding client transaction."));
            }

            NrAssert.Multiple(assertions.ToArray());

        }

        [Fact]
        public abstract void CAT_Metrics();

    }
}
#endif
