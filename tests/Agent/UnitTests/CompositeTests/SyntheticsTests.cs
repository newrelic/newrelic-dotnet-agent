// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Synthetics;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using NewRelic.Core;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Telerik.JustMock;

namespace CompositeTests
{
    // Test with distributed tracing both enabled (true) and disabled (false)
    [TestFixture(true)]
    [TestFixture(false)]
    internal class SyntheticsTests
    {
        private static CompositeTestAgent _compositeTestAgent;

        private const string SyntheticsHeaderKey = "X-NewRelic-Synthetics";

        private IAgent _agent;

        private bool _isDistributedTracingEnabled;

        public SyntheticsTests(bool isDistributedTracingEnabled)
        {
            _isDistributedTracingEnabled = isDistributedTracingEnabled;
        }

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();

            _compositeTestAgent.LocalConfiguration.distributedTracing.enabled = _isDistributedTracingEnabled;

            _agent = _compositeTestAgent.GetAgent();
        }

        [TearDown]
        public static void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        [Test]
        public void synthetics_attributes_when_header_is_present()
        {
            // ARRANGE
            var encodingKey = "foo";
            var version = 1;
            var clientAccountId = 123;
            var resourceId = "resourceId";
            var jobId = "jobId";
            var monitorId = "monitorId";
            _compositeTestAgent.ServerConfiguration.TrustedIds = new long[] { clientAccountId };
            _compositeTestAgent.ServerConfiguration.EncodingKey = encodingKey;
            _compositeTestAgent.PushConfiguration();

            var syntheticsHeaderValue = Strings.Base64Encode(
                    string.Format(@"[{0}, {1}, ""{2}"", ""{3}"", ""{4}""]", version, clientAccountId, resourceId, jobId,
                        monitorId), encodingKey);
            var requestHeaders = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>(SyntheticsHeaderKey, syntheticsHeaderValue)
            };

            // ==== ACT ====
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            tx.AcceptDistributedTraceHeaders(requestHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            segment.End();
            tx.End();
            _compositeTestAgent.Harvest();

            // ASSERT
            var unexpectedEventAttributes = new List<string>
            {
                "nr.alternatePathHashes"
            };
            var expectedEventAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "nr.guid"},
                new ExpectedAttribute {Key = "nr.syntheticsResourceId", Value = resourceId},
                new ExpectedAttribute {Key = "nr.syntheticsJobId", Value = jobId},
                new ExpectedAttribute {Key = "nr.syntheticsMonitorId", Value = monitorId}
            };
            var expectedTraceAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "synthetics_resource_id", Value = resourceId},
                new ExpectedAttribute {Key = "synthetics_job_id", Value = jobId},
                new ExpectedAttribute {Key = "synthetics_monitor_id", Value = monitorId}
            };
            var transactionEvent = _compositeTestAgent.TransactionEvents.First();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () =>
                    TransactionEventAssertions.HasAttributes(expectedEventAttributes, AttributeClassification.Intrinsics,
                        transactionEvent),
                () =>
                    TransactionEventAssertions.DoesNotHaveAttributes(unexpectedEventAttributes,
                        AttributeClassification.Intrinsics, transactionEvent),
                () =>
                    TransactionTraceAssertions.HasAttributes(expectedTraceAttributes, AttributeClassification.Intrinsics,
                        transactionTrace)
                );
        }

        [Test]
        public void synthetics_attributes_when_no_header_is_present()
        {
            // ARRANGE
            var encodingKey = "foo";
            _compositeTestAgent.ServerConfiguration.TrustedIds = new long[] { 123 };
            _compositeTestAgent.ServerConfiguration.EncodingKey = encodingKey;
            _compositeTestAgent.PushConfiguration();


            // ==== ACT ====
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(new KeyValuePair<string, string>[0], HeaderFunctions.GetHeaders, TransportType.HTTP);

            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            segment.End();
            tx.End();
            _compositeTestAgent.Harvest();

            // ASSERT
            var unexpectedEventAttributes = new List<string>
            {
                "nr.guid",
                "nr.syntheticsResourceId",
                "nr.syntheticsJobId",
                "nr.syntheticsMonitorId"
            };
            var unexpectedTraceAttributes = new List<string>
            {
                "synthetics_resource_id",
                "synthetics_job_id",
                "synthetics_monitor_id"
            };
            var transactionEvent = _compositeTestAgent.TransactionEvents.First();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => TransactionEventAssertions.DoesNotHaveAttributes(unexpectedEventAttributes, AttributeClassification.Intrinsics, transactionEvent),
                () => TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedTraceAttributes, AttributeClassification.Intrinsics, transactionTrace)
                );
        }

        [Test]
        public void synthetics_attributes_when_no_headers_are_provided()
        {
            // ARRANGE
            var encodingKey = "foo";
            _compositeTestAgent.ServerConfiguration.TrustedIds = new long[] { 123 };
            _compositeTestAgent.ServerConfiguration.EncodingKey = encodingKey;
            _compositeTestAgent.PushConfiguration();


            // ==== ACT ====
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            _agent.StartTransactionSegmentOrThrow("segmentName").End();
            tx.End();
            _compositeTestAgent.Harvest();

            // ASSERT
            var unexpectedEventAttributes = new List<string>
            {
                "nr.guid",
                "nr.syntheticsResourceId",
                "nr.syntheticsJobId",
                "nr.syntheticsMonitorId"
            };
            var unexpectedTraceAttributes = new List<string>
            {
                "synthetics_resource_id",
                "synthetics_job_id",
                "synthetics_monitor_id"
            };
            var transactionEvent = _compositeTestAgent.TransactionEvents.First();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => TransactionEventAssertions.DoesNotHaveAttributes(unexpectedEventAttributes, AttributeClassification.Intrinsics, transactionEvent),
                () => TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedTraceAttributes, AttributeClassification.Intrinsics, transactionTrace)
                );
        }

        [Test]
        public void synthetics_header_is_generated_when_outbound_request_is_made()
        {
            // ARRANGE
            var encodingKey = "foo";
            var version = 1;
            var clientAccountId = 123;
            var resourceId = "resourceId";
            var jobId = "jobId";
            var monitorId = "monitorId";
            _compositeTestAgent.ServerConfiguration.TrustedIds = new long[] { clientAccountId };
            _compositeTestAgent.ServerConfiguration.CatId = "123#456";
            _compositeTestAgent.ServerConfiguration.EncodingKey = encodingKey;
            _compositeTestAgent.PushConfiguration();

            var syntheticsHeaderValue = Strings.Base64Encode(
                    string.Format(@"[{0}, {1}, ""{2}"", ""{3}"", ""{4}""]", version, clientAccountId, resourceId, jobId,
                        monitorId), encodingKey);
            var requestHeaders = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>(SyntheticsHeaderKey, syntheticsHeaderValue)
            };

            // ==== ACT ====
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(requestHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            var headers = _agent.CurrentTransaction.GetRequestMetadata().ToDictionary();

            Assert.That(headers, Is.Not.Null);
            Assert.That(headers[SyntheticsHeaderKey], Is.EqualTo("PV5DV11cSk0dAxwAEx0MAyYLRENNDAANLwtNSk0CCQEGEgAdLwtNOw=="));
        }

        [Test]
        public void synthetics_header_can_be_decoded_from_outbound_request()
        {
            // ARRANGE
            var encodingKey = "foo";
            var version = 1;
            var clientAccountId = 123;
            var resourceId = "resourceId";
            var jobId = "jobId";
            var monitorId = "monitorId";
            _compositeTestAgent.ServerConfiguration.TrustedIds = new long[] { clientAccountId };
            _compositeTestAgent.ServerConfiguration.CatId = "123#456";
            _compositeTestAgent.ServerConfiguration.EncodingKey = encodingKey;
            _compositeTestAgent.PushConfiguration();

            var syntheticsHeaderValue = Strings.Base64Encode(
                string.Format(@"[{0}, {1}, ""{2}"", ""{3}"", ""{4}""]", version, clientAccountId, resourceId, jobId,
                monitorId), encodingKey);
            var requestHeaders = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>(SyntheticsHeaderKey, syntheticsHeaderValue)
            };

            // ==== ACT ====
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            tx.AcceptDistributedTraceHeaders(requestHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            var headers = _agent.CurrentTransaction.GetRequestMetadata().ToDictionary();

            Assert.That(headers, Is.Not.Null);

            var configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => configurationService.Configuration).Returns(() => _compositeTestAgent.CurrentConfiguration);

            var syntheticsHeaderHandler = new SyntheticsHeaderHandler(configurationService);
            SyntheticsHeader decodedSyntheticsHeader = syntheticsHeaderHandler.TryDecodeInboundRequestHeaders(headers, GetHeaderValue);

            NrAssert.Multiple(
                () => Assert.That(headers[SyntheticsHeaderKey], Is.EqualTo("PV5DV11cSk0dAxwAEx0MAyYLRENNDAANLwtNSk0CCQEGEgAdLwtNOw==")),
                () => Assert.That(version, Is.EqualTo(decodedSyntheticsHeader.Version)),
                () => Assert.That(jobId, Is.EqualTo(decodedSyntheticsHeader.JobId)),
                () => Assert.That(clientAccountId, Is.EqualTo(decodedSyntheticsHeader.AccountId)),
                () => Assert.That(monitorId, Is.EqualTo(decodedSyntheticsHeader.MonitorId)),
                () => Assert.That(resourceId, Is.EqualTo(decodedSyntheticsHeader.ResourceId))
            );

            List<string> GetHeaderValue(Dictionary<string, string> carrier, string key)
            {
                var headerValues = new List<string>();
                foreach (var item in carrier)
                {
                    if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        headerValues.Add(item.Value);
                    }
                }
                return headerValues;
            }
        }
    }
}
