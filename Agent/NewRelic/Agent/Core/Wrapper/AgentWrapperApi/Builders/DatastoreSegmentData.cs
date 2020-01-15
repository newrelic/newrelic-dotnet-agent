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

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
	public class DatastoreSegmentData : AbstractSegmentData
	{
		[CanBeNull]
		public String Operation { protected get; set; }
		public DatastoreVendor DatastoreVendorName { get; set; }
		[CanBeNull]
		public String Model { protected get; set; }
		[CanBeNull]
		public String CommandText { get; set; }
		[CanBeNull]
		public String Host { get; set; }
		[CanBeNull]
		public String PortPathOrId { get; set; }
		[CanBeNull]
		public String DatabaseName { get; set; }
		[CanBeNull]
		public Func<Object> GetExplainPlanResources { get; set; }
		[CanBeNull]
		public Func<Object, ExplainPlan> GenerateExplainPlan { get; set; }
		[CanBeNull]
		public Func<Boolean> DoExplainPlanCondition { get; set; }

		private Object _explainPlanResources;
		private ExplainPlan _explainPlan;
		public ExplainPlan ExplainPlan => _explainPlan;

		public DatastoreSegmentData()
		{
		}

		public DatastoreSegmentData(string operation, DatastoreVendor datastoreVendorName, string model, string commandText, ExplainPlan explainPlan, string host, string portPathOrId, string databaseName)
		{
			Operation = operation;
			DatastoreVendorName = datastoreVendorName;
			Model = model;
			CommandText = commandText;
			_explainPlan = explainPlan;
			Host = host;
			PortPathOrId = portPathOrId;
			DatabaseName = databaseName;
		}

		internal override void AddTransactionTraceParameters(IConfigurationService configurationService, Segment segment, IDictionary<string, object> segmentParameters, ImmutableTransaction immutableTransaction)
		{
			if (ExplainPlan != null)
			{
				segmentParameters["explain_plan"] = new ExplainPlanWireModel(ExplainPlan);
			}

			if (CommandText != null)
			{
				segmentParameters["sql"] = immutableTransaction.GetSqlObfuscatedAccordingToConfig(CommandText, DatastoreVendorName);
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
				Log.DebugFormat("Unable to retrieve resources for explain plan: {0}", exception);
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
								data[index] = obfuscator.GetObfuscatedSql(data[index].ToString(), DatastoreVendorName);
							}
						}

						_explainPlan = new ExplainPlan(explainPlan.ExplainPlanHeaders, explainPlan.ExplainPlanDatas, explainPlan.ObfuscatedHeaders);
					}
				}
			}
			catch (Exception exception)
			{
				Log.DebugFormat("Unable to generate explain plan: {0}", exception);
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
				MetricBuilder.TryBuildDatastoreStatementMetric(DatastoreVendorName, Model, Operation, duration, exclusiveDuration, txStats);
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
