using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	public interface ISqlTraceMaker
	{
		SqlTraceWireModel TryGetSqlTrace(ImmutableTransaction immutableTransaction, TransactionMetricName transactionMetricName, Segment segment);
	}


	public class SqlTraceMaker : ISqlTraceMaker
	{
		private readonly IConfigurationService _configurationService;
		private readonly IAttributeService _attributeService;
		private readonly IDatabaseService _databaseService;

		public SqlTraceMaker(IConfigurationService configurationService, IAttributeService attributeService, IDatabaseService databaseService)
		{
			_configurationService = configurationService;
			_attributeService = attributeService;
			_databaseService = databaseService;
		}

		public SqlTraceWireModel TryGetSqlTrace(ImmutableTransaction immutableTransaction, TransactionMetricName transactionMetricName, Segment segment)
		{
			var segmentData = segment.Data as DatastoreSegmentData;

			if (segment.Duration == null || segmentData == null)
				return null;

			var transactionName = transactionMetricName.PrefixedName;
			var uri = immutableTransaction.TransactionMetadata.Uri ?? "<unknown>";

			if (!_attributeService.AllowRequestUri(AttributeDestinations.SqlTrace))
			{
				uri = "<unknown>";
			}

			var sql = _databaseService.GetObfuscatedSql(segmentData.CommandText, segmentData.DatastoreVendorName);
			var sqlId = _databaseService.GetSqlId(segmentData.CommandText,segmentData.DatastoreVendorName);

			var metricName = segmentData.GetTransactionTraceName();
			const int count = 1;
			var totalCallTime = segment.Duration.Value;
			var parameterData = new Dictionary<string, object>(); // Explain plans will go here

			if (segmentData.ExplainPlan != null)
			{
				parameterData.Add("explain_plan", new ExplainPlanWireModel(segmentData.ExplainPlan));
			}

			if (_configurationService.Configuration.InstanceReportingEnabled)
			{
				parameterData.Add("host", segmentData.Host);
				parameterData.Add("port_path_or_id", segmentData.PortPathOrId);
			}

			if (_configurationService.Configuration.DatabaseNameReportingEnabled)
			{
				parameterData.Add("database_name", segmentData.DatabaseName);
			}

			if (segmentData.QueryParameters != null)
			{
				parameterData["query_parameters"] = segmentData.QueryParameters;
			}

			var sqlTraceData = new SqlTraceWireModel(transactionName, uri, sqlId, sql, metricName, count, totalCallTime,
				totalCallTime, totalCallTime, parameterData);
			return sqlTraceData;
		}
	}
}
