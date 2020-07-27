using System.Collections.Generic;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Commands;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface IDataTransportService
    {
        IEnumerable<CommandModel> GetAgentCommands();
        void SendCommandResults(IDictionary<string, object> commandResults);
        void SendThreadProfilingData(IEnumerable<ThreadProfilingModel> threadProfilingData);
        DataTransportResponseStatus Send(IEnumerable<TransactionTraceWireModel> transactionSampleDatas);
        DataTransportResponseStatus Send(IEnumerable<ErrorTraceWireModel> errorTraceDatas);

        DataTransportResponseStatus Send(IEnumerable<MetricWireModel> metrics);
        DataTransportResponseStatus Send(IEnumerable<TransactionEventWireModel> transactionEvents);
        DataTransportResponseStatus Send(ErrorEventAdditions additions, IEnumerable<ErrorEventWireModel> errorEvents);

        DataTransportResponseStatus Send(IEnumerable<SqlTraceWireModel> sqlTraceWireModels);
        DataTransportResponseStatus Send(IEnumerable<CustomEventWireModel> customEvents);
    }
}
