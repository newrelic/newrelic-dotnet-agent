using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Metric;
using NewRelic.Core.Logging;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Api
{
	public class AgentBridgeApi
	{
		private readonly IAgent _agent;
		private readonly IApiSupportabilityMetricCounters _apiSupportabilityMetricCounters;

		public AgentBridgeApi(IAgent agent, IApiSupportabilityMetricCounters apiSupportabilityMetricCounters)
		{
			_agent = agent;
			_apiSupportabilityMetricCounters = apiSupportabilityMetricCounters;
		}

		public TransactionBridgeApi CurrentTransaction
		{
			get
			{
				try
				{
					using (new IgnoreWork())
					{
						_apiSupportabilityMetricCounters.Record(ApiMethod.CurrentTransaction);
						var transaction = _agent.CurrentTransaction;
						return new TransactionBridgeApi(transaction, _apiSupportabilityMetricCounters);
					}
				}
				catch (Exception ex)
				{
					try
					{
						Log.ErrorFormat("Failed to get CurrentTransaction: {0}", ex);
					}
					catch (Exception)
					{
						//Swallow the error
					}
					return null;
				}
			}
		}

		public object TraceMetadata
		{
			get
			{
				try
				{
					using (new IgnoreWork())
					{
						_apiSupportabilityMetricCounters.Record(ApiMethod.TraceMetadata);
						return _agent.TraceMetadata;
					}
				}
				catch (Exception ex)
				{
					try
					{
						Log.ErrorFormat("Failed to get TraceMetadata: {0}", ex);
					}
					catch (Exception)
					{
						//Swallow the error
					}
					return null;
				}

			}
		}

		public Dictionary<string, string> GetLinkingMetadata()
		{
			try
			{
				using (new IgnoreWork())
				{
					_apiSupportabilityMetricCounters.Record(ApiMethod.GetLinkingMetadata);
					return _agent.GetLinkingMetadata();
				}
			}
			catch (Exception ex)
			{
				try
				{
					Log.ErrorFormat("Error in GetLinkingMetadata: {0}", ex);
				}
				catch (Exception)
				{
					//Swallow the error
				}

				return null;
			}

		}
	}
}