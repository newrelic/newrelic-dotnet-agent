using System;
using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.IntegrationTests.CatInbound
{
    public static class Expectations
    {
        #region CAT Disabled

        public static readonly IEnumerable<string> UnexpectedTransactionTraceIntrinsicAttributesCatDisabled = new List<string>
        {
            "client_cross_process_id",
            "referring_transaction_guid",
            "path_hash"
        };

        public static readonly IEnumerable<string> UnexpectedTransactionEventIntrinsicAttributesCatDisabled = new List<string>
        {
            "nr.referringPathHash",
            "nr.referringTransactionGuid",
            "nr.alternatePathHashes",
            "nr.guid",
            "nr.pathHash"
        };

        public static readonly IEnumerable<Assertions.ExpectedMetric> UnexpectedMetricsCatDisabled = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric {metricName = @"ClientApplication/[^/]+/all", IsRegexName = true}
        };

        #endregion CAT Disabled

        #region CAT Enabled

        public static readonly IEnumerable<string> ExpectedTransactionTraceIntrinsicAttributesCatEnabled = new List<string>
        {
            "client_cross_process_id",
            "trip_id",
            "path_hash"
        };

        public static readonly IEnumerable<string> UnexpectedTransactionTraceIntrinsicAttributesCatEnabled = new List<string>
        {
            "referring_transaction_guid"
        };

        public static readonly IEnumerable<string> ExpectedTransactionEventIntrinsicAttributesCatEnabled = new List<string>
        {
            "nr.guid",
            "nr.tripId",
            "nr.pathHash"
        };

        public static readonly IEnumerable<string> UnexpectedTransactionEventIntrinsicAttributesCatEnabled = new List<string>
        {
            "nr.referringPathHash",
            "nr.referringTransactionGuid",
            "nr.alternatePathHashes"
        };

        public static readonly IEnumerable<Assertions.ExpectedMetric> ExpectedMetricsCatEnabled = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric {metricName = @"ClientApplication/[^/]+/all", IsRegexName = true}
        };

        #endregion CAT Enabled
    }
}
