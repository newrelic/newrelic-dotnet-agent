using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Commands;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Exceptions;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.SystemInterfaces;

namespace NewRelic.Agent.Core.DataTransport
{
    public class DataTransportService : ConfigurationBasedService, IDataTransportService
    {
        private readonly IConnectionManager _connectionManager;
        private readonly IDateTimeStatic _dateTimeStatic;

        private DateTime _lastMetricSendTime;

        public DataTransportService(IConnectionManager connectionManager, IDateTimeStatic dateTimeStatic)
        {
            _connectionManager = connectionManager;
            _dateTimeStatic = dateTimeStatic;
            _lastMetricSendTime = _dateTimeStatic.UtcNow;
        }

        #region Public API

        public IEnumerable<CommandModel> GetAgentCommands()
        {
            var response = TrySendDataRequest<List<CommandModel>>("get_agent_commands", _configuration.AgentRunId);
            if (response.Status != DataTransportResponseStatus.RequestSuccessful || response.ReturnValue == null)
                return Enumerable.Empty<CommandModel>();

            return response.ReturnValue;
        }

        public void SendCommandResults(IDictionary<String, Object> commandResults)
        {
            TrySendDataRequest("agent_command_results", _configuration.AgentRunId, commandResults);
        }

        public void SendThreadProfilingData(IEnumerable<ThreadProfilingModel> threadProfilingData)
        {
            TrySendDataRequest("profile_data", _configuration.AgentRunId, threadProfilingData);
        }

        public DataTransportResponseStatus Send(IEnumerable<TransactionEventWireModel> transactionEvents)
        {
            var response = TrySendDataRequest("analytic_event_data", _configuration.AgentRunId, transactionEvents);
            return response.Status;
        }

        public DataTransportResponseStatus Send(ErrorEventAdditions additions, IEnumerable<ErrorEventWireModel> errorEvents)
        {
            var response = TrySendDataRequest("error_event_data", _configuration.AgentRunId, additions, errorEvents);
            return response.Status;
        }

        public DataTransportResponseStatus Send(IEnumerable<CustomEventWireModel> customEvents)
        {
            var response = TrySendDataRequest("custom_event_data", _configuration.AgentRunId, customEvents);
            return response.Status;
        }

        public DataTransportResponseStatus Send(IEnumerable<TransactionTraceWireModel> transactionSampleDatas)
        {
            var response = TrySendDataRequest("transaction_sample_data", _configuration.AgentRunId, transactionSampleDatas);
            return response.Status;
        }

        public DataTransportResponseStatus Send(IEnumerable<ErrorTraceWireModel> errorTraceDatas)
        {
            var response = TrySendDataRequest("error_data", _configuration.AgentRunId, errorTraceDatas);
            return response.Status;
        }

        public DataTransportResponseStatus Send(IEnumerable<SqlTraceWireModel> sqlTraceWireModels)
        {
            var response = TrySendDataRequest("sql_trace_data", sqlTraceWireModels);
            return response.Status;
        }

        public DataTransportResponseStatus Send(IEnumerable<MetricWireModel> metrics)
        {
            if (!metrics.Any())
                return DataTransportResponseStatus.RequestSuccessful;

            var beginTime = _lastMetricSendTime;
            var endTime = _dateTimeStatic.UtcNow;
            if (beginTime >= endTime)
            {
                Log.ErrorFormat("The last data send timestamp ({0}) is greater than or equal to the current timestamp ({1}). The metrics in this batch will be dropped.", _lastMetricSendTime, endTime);
                _lastMetricSendTime = _dateTimeStatic.UtcNow;
                return DataTransportResponseStatus.OtherError;
            }

            var response = TrySendDataRequest("metric_data", _configuration.AgentRunId, beginTime.ToUnixTimeSeconds(), endTime.ToUnixTimeSeconds(), metrics);

            if (response.Status == DataTransportResponseStatus.RequestSuccessful)
                _lastMetricSendTime = endTime;

            return response.Status;
        }

        #endregion Public API

        #region Private helpers


        private DataTransportResponse<Object> TrySendDataRequest(String method, params Object[] data)
        {
            return TrySendDataRequest<Object>(method, data);
        }

        private DataTransportResponse<T> TrySendDataRequest<T>(String method, params Object[] data)
        {
            try
            {
                var returnValue = _connectionManager.SendDataRequest<T>(method, data);
                return new DataTransportResponse<T>(DataTransportResponseStatus.RequestSuccessful, returnValue);
            }
            catch (LicenseException ex)
            {
                Log.Error("The license key configured is invalid. Please check that a valid license key is configured. For customers in the European Union, this version of the agent is not supported. Please use the latest version of the New Relic .NET Agent.");
                return Shutdown<T>(ex.Message);
            }
            catch (ForceDisconnectException ex)
            {
                return Shutdown<T>(ex.Message);
            }
            catch (ForceRestartException)
            {
                return Restart<T>();
            }
            catch (ConnectionException ex)
            {
                return GetErrorResponse<T>(ex, DataTransportResponseStatus.ConnectionError);
            }
            catch (PostTooBigException ex)
            {
                return GetErrorResponse<T>(ex, DataTransportResponseStatus.PostTooBigError);
            }
            catch (ServiceUnavailableException ex)
            {
                return GetErrorResponse<T>(ex, DataTransportResponseStatus.ServiceUnavailableError);
            }
            catch (Exception ex)
            {
                return GetErrorResponse<T>(ex, DataTransportResponseStatus.OtherError);
            }
        }

        private static DataTransportResponse<T> Shutdown<T>(String message)
        {
            Log.InfoFormat("Shutting down: {0}", message);
            EventBus<KillAgentEvent>.Publish(new KillAgentEvent());
            return new DataTransportResponse<T>(DataTransportResponseStatus.OtherError);
        }

        private static DataTransportResponse<T> Restart<T>()
        {
            EventBus<RestartAgentEvent>.Publish(new RestartAgentEvent());
            return new DataTransportResponse<T>(DataTransportResponseStatus.OtherError);
        }

        private static DataTransportResponse<T> GetErrorResponse<T>(Exception exception, DataTransportResponseStatus errorStatus)
        {
            Log.Error(exception);
            return new DataTransportResponse<T>(errorStatus);
        }

        #endregion Private helpers

        #region Event handlers

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
            // If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).
        }

        #endregion Event handlers
    }
}
