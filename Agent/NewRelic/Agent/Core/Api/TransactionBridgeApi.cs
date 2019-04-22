using System;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Api
{
	public class TransactionBridgeApi
	{
		public static readonly TransportType[] TransportTypeMapping = new[]
		{
			TransportType.Unknown,
			TransportType.HTTP,
			TransportType.HTTPS,
			TransportType.Kafka,
			TransportType.JMS,
			TransportType.IronMQ,
			TransportType.AMQP,
			TransportType.Queue,
			TransportType.Other
		};

		private readonly ITransaction _transaction;
		private readonly IApiSupportabilityMetricCounters _apiSupportabilityMetricCounters;

		public TransactionBridgeApi(ITransaction transaction, IApiSupportabilityMetricCounters apiSupportabilityMetricCounters)
		{
			_transaction = transaction;
			_apiSupportabilityMetricCounters = apiSupportabilityMetricCounters;
		}

		public object CreateDistributedTracePayload()
		{
			try
			{
				using (new IgnoreWork())
				{
					_apiSupportabilityMetricCounters.Record(ApiMethod.CreateDistributedTracePayload);
					return _transaction.CreateDistributedTracePayload();
				}
			}
			catch (Exception ex)
			{
				try
				{
					Log.ErrorFormat("Failed to create distributed trace payload: {0}", ex);
				}
				catch (Exception)
				{
					//Swallow the error
				}
				return null;
			}
		}

		public void AcceptDistributedTracePayload(string payload, int transportType)
		{
			try
			{
				using (new IgnoreWork())
				{
					_apiSupportabilityMetricCounters.Record(ApiMethod.AcceptDistributedTracePayload);
					_transaction.AcceptDistributedTracePayload(payload, GetTransportTypeValue(transportType));
				}
			}
			catch (Exception ex)
			{
				try
				{
					Log.ErrorFormat("Error in AcceptDistributedTracePayload(string): {0}", ex);
				}
				catch (Exception)
				{
					//Swallow the error
				}
			}
		}

		private static TransportType GetTransportTypeValue(int transportType)
		{
			if (transportType >= 0 && transportType < TransportTypeMapping.Length)
			{
				return TransportTypeMapping[transportType];
			}

			return TransportType.Unknown;
		}
	}
}