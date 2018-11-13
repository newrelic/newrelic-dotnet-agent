using System;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Api
{
	public class AgentBridgeApi
	{
		private readonly IAgentWrapperApi _agentWrapperApi;
		private readonly IApiSupportabilityMetricCounters _apiSupportabilityMetricCounters;

		public AgentBridgeApi(IAgentWrapperApi agentWrapperApi, IApiSupportabilityMetricCounters apiSupportabilityMetricCounters)
		{
			_agentWrapperApi = agentWrapperApi;
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
						var transactionWrapperApi = _agentWrapperApi.CurrentTransactionWrapperApi;
						return new TransactionBridgeApi(transactionWrapperApi, _apiSupportabilityMetricCounters);
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