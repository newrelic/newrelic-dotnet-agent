using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Commands;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.DataTransport
{
	public interface IDataTransportService
	{
		[NotNull]
		IEnumerable<CommandModel> GetAgentCommands();
		void SendCommandResults([NotNull] IDictionary<String, Object> commandResults);
		void SendThreadProfilingData([NotNull] IEnumerable<ThreadProfilingModel> threadProfilingData);
		DataTransportResponseStatus Send([NotNull] IEnumerable<TransactionTraceWireModel> transactionSampleDatas);
		DataTransportResponseStatus Send([NotNull] IEnumerable<ErrorTraceWireModel> errorTraceDatas);

		DataTransportResponseStatus Send([NotNull] IEnumerable<MetricWireModel> metrics);
		DataTransportResponseStatus Send([NotNull] IEnumerable<TransactionEventWireModel> transactionEvents);
		DataTransportResponseStatus Send(ErrorEventAdditions additions, IEnumerable<ErrorEventWireModel> errorEvents);

		DataTransportResponseStatus Send([NotNull] IEnumerable<SqlTraceWireModel> sqlTraceWireModels);
		DataTransportResponseStatus Send([NotNull] IEnumerable<CustomEventWireModel> customEvents);
	}
}
