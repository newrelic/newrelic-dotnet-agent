using System.Collections.Generic;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
    public interface ISqlTraceMaker
    {
        [CanBeNull]
        SqlTraceWireModel TryGetSqlTrace(ImmutableTransaction immutableTransaction, TransactionMetricName transactionMetricName, TypedSegment<DatastoreSegmentData> segment);
    }


    public class SqlTraceMaker : ISqlTraceMaker
    {
        [NotNull] private readonly IConfigurationService _configurationService;

        public SqlTraceMaker(IConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        [CanBeNull]
        public SqlTraceWireModel TryGetSqlTrace([NotNull] ImmutableTransaction immutableTransaction, [NotNull] TransactionMetricName transactionMetricName, [NotNull] TypedSegment<DatastoreSegmentData> segment)
        {
            if (segment.Duration == null)
                return null;

            var segmentData = segment.TypedData;
            var transactionName = transactionMetricName.PrefixedName;
            var uri = immutableTransaction.TransactionMetadata.Uri ?? "<unknown>";
            var sql = immutableTransaction.GetSqlObfuscatedAccordingToConfig(segmentData.CommandText, segmentData.DatastoreVendorName);
            var sqlId = immutableTransaction.GetSqlId(segmentData.CommandText, segmentData.DatastoreVendorName);

            var metricName = segment.GetTransactionTraceName();
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

            var sqlTraceData = new SqlTraceWireModel(transactionName, uri, sqlId, sql, metricName, count, totalCallTime,
                totalCallTime, totalCallTime, parameterData);
            return sqlTraceData;
        }
    }
}
