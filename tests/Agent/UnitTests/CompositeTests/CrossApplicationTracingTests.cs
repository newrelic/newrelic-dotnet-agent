// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using NewRelic.Core;
using NewRelic.Testing.Assertions;

namespace CompositeTests
{
    internal class CrossApplicationTracingTests
    {
        private static CompositeTestAgent _compositeTestAgent;

        private IAgent _agent;

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _compositeTestAgent.LocalConfiguration.distributedTracing.enabled = false;
            _compositeTestAgent.PushConfiguration();

            _agent = _compositeTestAgent.GetAgent();
        }

        [TearDown]
        public static void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        [Test]
        public void cat_attributes_when_header_is_present()
        {
            // ARRANGE
            var encodingKey = "foo";
            var clientAccountId = 123;
            var crossProcessId = $"{clientAccountId}#456";
            var clientTransactionGuid = "transaction guid";
            var clientTripId = "trip id";
            var clientPathHash = "path hash";
            _compositeTestAgent.ServerConfiguration.TrustedIds = new long[] { clientAccountId };
            _compositeTestAgent.ServerConfiguration.EncodingKey = encodingKey;
            _compositeTestAgent.PushConfiguration();

            var newRelicIdHeader = new KeyValuePair<string, string>("X-NewRelic-ID", Strings.Base64Encode(crossProcessId, encodingKey));
            var newRelicTransactionHeader = new KeyValuePair<string, string>("X-NewRelic-Transaction", Strings.Base64Encode($@"[""{clientTransactionGuid}"", ""{false}"", ""{clientTripId}"", ""{clientPathHash}""]", encodingKey));
            var requestHeaders = new[] { newRelicIdHeader, newRelicTransactionHeader };

            // ==== ACT ====
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(requestHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

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
                new ExpectedAttribute {Key = "nr.tripId", Value = clientTripId},
                new ExpectedAttribute {Key = "nr.pathHash", Value = "b5880367"},
                new ExpectedAttribute {Key = "nr.referringTransactionGuid", Value = clientTransactionGuid},
                new ExpectedAttribute {Key = "nr.referringPathHash", Value = "path hash"}
            };

            var expectedTraceAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "client_cross_process_id", Value = crossProcessId},
                new ExpectedAttribute {Key = "trip_id", Value = clientTripId},
                new ExpectedAttribute {Key = "path_hash", Value = "b5880367"},
                new ExpectedAttribute {Key = "referring_transaction_guid", Value = clientTransactionGuid},
            };

            var transactionEvent = _compositeTestAgent.TransactionEvents.First();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple
            (
                () => TransactionEventAssertions.HasAttributes(expectedEventAttributes, AttributeClassification.Intrinsics, transactionEvent),
                () => TransactionEventAssertions.DoesNotHaveAttributes(unexpectedEventAttributes, AttributeClassification.Intrinsics, transactionEvent),
                () => TransactionTraceAssertions.HasAttributes(expectedTraceAttributes, AttributeClassification.Intrinsics, transactionTrace)
            );
        }

        [Test]
        public void cat_attributes_when_header_is_present_case_insensitive()
        {
            // ARRANGE
            var encodingKey = "foo";
            var clientAccountId = 123;
            var crossProcessId = $"{clientAccountId}#456";
            var clientTransactionGuid = "transaction guid";
            var clientTripId = "trip id";
            var clientPathHash = "path hash";
            _compositeTestAgent.ServerConfiguration.TrustedIds = new long[] { clientAccountId };
            _compositeTestAgent.ServerConfiguration.EncodingKey = encodingKey;
            _compositeTestAgent.PushConfiguration();

            var newRelicIdHeader = new KeyValuePair<string, string>("x-NeWrElIc-Id", Strings.Base64Encode(crossProcessId, encodingKey));
            var newRelicTransactionHeader = new KeyValuePair<string, string>("X-NeWrElIc-TranSACTion", Strings.Base64Encode($@"[""{clientTransactionGuid}"", ""{false}"", ""{clientTripId}"", ""{clientPathHash}""]", encodingKey));
            var requestHeaders = new[] { newRelicIdHeader, newRelicTransactionHeader };

            // ==== ACT ====
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(requestHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

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
                new ExpectedAttribute {Key = "nr.tripId", Value = clientTripId},
                new ExpectedAttribute {Key = "nr.pathHash", Value = "b5880367"},
                new ExpectedAttribute {Key = "nr.referringTransactionGuid", Value = clientTransactionGuid},
                new ExpectedAttribute {Key = "nr.referringPathHash", Value = "path hash"}
            };
            var expectedTraceAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "client_cross_process_id", Value = crossProcessId},
                new ExpectedAttribute {Key = "trip_id", Value = clientTripId},
                new ExpectedAttribute {Key = "path_hash", Value = "b5880367"},
                new ExpectedAttribute {Key = "referring_transaction_guid", Value = clientTransactionGuid},
            };
            var transactionEvent = _compositeTestAgent.TransactionEvents.First();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => TransactionEventAssertions.HasAttributes(expectedEventAttributes, AttributeClassification.Intrinsics, transactionEvent),
                () => TransactionEventAssertions.DoesNotHaveAttributes(unexpectedEventAttributes, AttributeClassification.Intrinsics, transactionEvent),
                () => TransactionTraceAssertions.HasAttributes(expectedTraceAttributes, AttributeClassification.Intrinsics, transactionTrace)
                );
        }

        [Test]
        public void cat_attributes_when_no_header_is_present()
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

            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            segment.End();
            tx.End();
            _compositeTestAgent.Harvest();

            // ASSERT
            var unexpectedEventAttributes = new List<string>
            {
                "nr.referringPathHash",
                "nr.referringTransactionGuid",
                "nr.alternatePathHashes",
                "nr.guid",
                "nr.pathHash"
            };
            var unexpectedTraceAttributes = new List<string>
            {
                "client_cross_process_id",
                "referring_transaction_guid",
                "path_hash"
            };
            var transactionEvent = _compositeTestAgent.TransactionEvents.First();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => TransactionEventAssertions.DoesNotHaveAttributes(unexpectedEventAttributes, AttributeClassification.Intrinsics, transactionEvent),
                () => TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedTraceAttributes, AttributeClassification.Intrinsics, transactionTrace)
                );
        }

        [Test]
        public void cat_attributes_when_no_headers_are_provided()
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
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            segment.End();
            tx.End();
            _compositeTestAgent.Harvest();

            // ASSERT
            var unexpectedEventAttributes = new List<string>
            {
                "nr.referringPathHash",
                "nr.referringTransactionGuid",
                "nr.alternatePathHashes",
                "nr.guid",
                "nr.pathHash"
            };
            var unexpectedTraceAttributes = new List<string>
            {
                "client_cross_process_id",
                "referring_transaction_guid",
                "path_hash"
            };
            var transactionEvent = _compositeTestAgent.TransactionEvents.First();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => TransactionEventAssertions.DoesNotHaveAttributes(unexpectedEventAttributes, AttributeClassification.Intrinsics, transactionEvent),
                () => TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedTraceAttributes, AttributeClassification.Intrinsics, transactionTrace)
                );
        }
    }
}
