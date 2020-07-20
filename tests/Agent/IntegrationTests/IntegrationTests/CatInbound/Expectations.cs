using System;
using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.IntegrationTests.CatInbound
{
	public static class Expectations
	{
		#region CAT Disabled

		public static readonly IEnumerable<String> UnexpectedTransactionTraceIntrinsicAttributesCatDisabled = new List<String>
		{
			"client_cross_process_id",
			"referring_transaction_guid",
			"path_hash"
		};

		public static readonly IEnumerable<String> UnexpectedTransactionEventIntrinsicAttributesCatDisabled = new List<String>
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

		public static readonly IEnumerable<String> ExpectedTransactionTraceIntrinsicAttributesCatEnabled = new List<String>
		{
			"client_cross_process_id",
			"trip_id",
			"path_hash"
		};

		public static readonly IEnumerable<String> UnexpectedTransactionTraceIntrinsicAttributesCatEnabled = new List<String>
		{
			"referring_transaction_guid"
		};
		
		public static readonly IEnumerable<String> ExpectedTransactionEventIntrinsicAttributesCatEnabled = new List<String>
		{
			"nr.guid",
			"nr.tripId",
			"nr.pathHash"
		};

		public static readonly IEnumerable<String> UnexpectedTransactionEventIntrinsicAttributesCatEnabled = new List<String>
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
