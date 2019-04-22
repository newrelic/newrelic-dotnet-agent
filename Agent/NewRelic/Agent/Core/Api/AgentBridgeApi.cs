using System;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Extensions.Providers.Wrapper;

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
	}
}