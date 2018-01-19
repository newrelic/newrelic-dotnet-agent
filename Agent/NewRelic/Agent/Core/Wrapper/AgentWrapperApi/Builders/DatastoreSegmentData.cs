using System;
using System.Collections.Generic;
using System.Data;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Time;
using static NewRelic.Agent.Core.WireModels.MetricWireModel;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Parsing.ConnectionString;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
	public class DatastoreSegmentData : AbstractSegmentData
	{
		private readonly static ConnectionInfo EmptyConnectionInfo = new ConnectionInfo(null, null, null);

		[CanBeNull]
		public String Operation => _parsedSqlStatement?.Operation;
		public DatastoreVendor DatastoreVendorName => _parsedSqlStatement.DatastoreVendor;
		[CanBeNull]
		public String Model => _parsedSqlStatement?.Model;
		[CanBeNull]
		public String CommandText { get; set; }
		[CanBeNull]
		public String Host => _connectionInfo.Host;
		[CanBeNull]
		public String PortPathOrId => _connectionInfo.PortPathOrId;
		[CanBeNull]
		public String DatabaseName => _connectionInfo.DatabaseName;
		[CanBeNull]
		public Func<Object> GetExplainPlanResources { get; set; }
		[CanBeNull]
		public Func<Object, ExplainPlan> GenerateExplainPlan { get; set; }
		[CanBeNull]
		public Func<Boolean> DoExplainPlanCondition { protected get; set; }

		private Object _explainPlanResources;
		private ExplainPlan _explainPlan;

		private ConnectionInfo _connectionInfo;
		private ParsedSqlStatement _parsedSqlStatement;

		public ExplainPlan ExplainPlan => _explainPlan;

		public DatastoreSegmentData(ParsedSqlStatement parsedSqlStatement, string commandText = null, ConnectionInfo connectionInfo = null)
		{
			this._connectionInfo = connectionInfo ?? EmptyConnectionInfo;
			this._parsedSqlStatement = parsedSqlStatement;
			CommandText = commandText;
		}

		internal override void AddTransactionTraceParameters(IConfigurationService configurationService, Segment segment, IDictionary<string, object> segmentParameters, ImmutableTransaction immutableTransaction)
		{
			if (ExplainPlan != null)
			{
				segmentParameters["explain_plan"] = new ExplainPlanWireModel(ExplainPlan);
			}

			if (CommandText != null)
			{
				segmentParameters["sql"] = immutableTransaction.GetSqlObfuscatedAccordingToConfig(CommandText);
			}

			if (configurationService.Configuration.InstanceReportingEnabled)
			{
				segmentParameters["host"] = Host;
				segmentParameters["port_path_or_id"] = PortPathOrId;
			}

			if (configurationService.Configuration.DatabaseNameReportingEnabled)
			{
				segmentParameters["database_name"] = DatabaseName;
			}
		}

		internal override IEnumerable<KeyValuePair<String, Object>> Finish()
		{
			if (GetExplainPlanResources == null)
				return null;

			// Ensures we aren't running explain plan twice
			if (_explainPlanResources != null)
				return null;

			try
			{
				// Using invoke for thread safety, DoExplainPlanCondition is nullable
				if (DoExplainPlanCondition?.Invoke() == true)
				{
					_explainPlanResources = GetExplainPlanResources();
				}
				else
				{
					GetExplainPlanResources = null;
					GenerateExplainPlan = null;
				}
			}
			catch (Exception exception)
			{
				Log.DebugFormat("Unable to retrieve resources for explain plan: \"{0}\", error: {1}",
					((IDbCommand)_explainPlanResources)?.CommandText, exception);
			}
			return null;
		}


		public void ExecuteExplainPlan(SqlObfuscator obfuscator)
		{
			// Don't re-run an explain plan if one already exists
			if (_explainPlan != null)
				return;

			try
			{
				// Using invoke for thread safety, DoExplainPlanCondition is nullable
				if (DoExplainPlanCondition?.Invoke() == true)
				{
					var explainPlan = GenerateExplainPlan?.Invoke(_explainPlanResources);
					if (explainPlan != null)
					{
						foreach (var data in explainPlan.ExplainPlanDatas)
						{
							foreach (var index in explainPlan.ObfuscatedHeaders)
							{
								data[index] = obfuscator.GetObfuscatedSql(data[index].ToString());
							}
						}

						_explainPlan = new ExplainPlan(explainPlan.ExplainPlanHeaders, explainPlan.ExplainPlanDatas, explainPlan.ObfuscatedHeaders);
					}
				}
			}
			catch (Exception exception)
			{
				Log.DebugFormat("Unable to execute explain plan: \"{0}\", error: {1}",
					((IDbCommand)_explainPlanResources)?.CommandText, exception);
			}
		}

		public override bool IsCombinableWith(AbstractSegmentData otherSegment)
		{

			var otherTypedSegment = otherSegment as DatastoreSegmentData;
			if (otherTypedSegment == null)
				return false;

			if (Operation != otherTypedSegment.Operation)
				return false;

			if (DatastoreVendorName != otherTypedSegment.DatastoreVendorName)
				return false;

			if (Model != otherTypedSegment.Model)
				return false;

			return true;
		}

		public override string GetTransactionTraceName()
		{
			var name = (Model == null) ? MetricNames.GetDatastoreOperation(DatastoreVendorName, Operation) : MetricNames.GetDatastoreStatement(DatastoreVendorName, Model, Operation);
			return name.ToString();
		}

		public override void AddMetricStats(Segment segment, TimeSpan durationOfChildren, TransactionMetricStatsCollection txStats, IConfigurationService configService)
		{
			var duration = segment.Duration.Value;
			var exclusiveDuration = TimeSpanMath.Max(TimeSpan.Zero, duration - durationOfChildren);

			if (!String.IsNullOrEmpty(Model))
			{
				MetricBuilder.TryBuildDatastoreStatementMetric(DatastoreVendorName, _parsedSqlStatement, duration, exclusiveDuration, txStats);
				MetricBuilder.TryBuildDatastoreVendorOperationMetric(DatastoreVendorName, Operation, duration, exclusiveDuration, txStats, true);
			}
			else
			{
				MetricBuilder.TryBuildDatastoreVendorOperationMetric(DatastoreVendorName, Operation, duration, exclusiveDuration, txStats, false);
			}

			MetricBuilder.TryBuildDatastoreRollupMetrics(DatastoreVendorName, duration, exclusiveDuration, txStats);

			if (configService.Configuration.InstanceReportingEnabled)
			{
				MetricBuilder.TryBuildDatastoreInstanceMetric(DatastoreVendorName, Host,
				PortPathOrId, duration, duration, txStats);
			}
		}

		public override Segment CreateSimilar(Segment segment, TimeSpan newRelativeStartTime, TimeSpan newDuration, [NotNull] IEnumerable<KeyValuePair<string, object>> newParameters)
		{
			return new TypedSegment<DatastoreSegmentData>(newRelativeStartTime, newDuration, segment, newParameters);
		}
	}
}
