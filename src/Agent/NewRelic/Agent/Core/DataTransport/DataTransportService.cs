// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Commands;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Core;
using NewRelic.Core.Logging;
using NewRelic.SystemInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface IDataTransportService
    {
        IEnumerable<CommandModel> GetAgentCommands();
        void SendCommandResults(IDictionary<string, object> commandResults);
        void SendThreadProfilingData(IEnumerable<ThreadProfilingModel> threadProfilingData);
        DataTransportResponseStatus Send(IEnumerable<TransactionTraceWireModel> transactionSampleDatas, string transactionId);
        DataTransportResponseStatus Send(IEnumerable<ErrorTraceWireModel> errorTraceDatas, string transactionId);
        DataTransportResponseStatus Send(IEnumerable<MetricWireModel> metrics, string transactionId);
        DataTransportResponseStatus Send(EventHarvestData eventHarvestData, IEnumerable<TransactionEventWireModel> transactionEvents, string transactionId);
        DataTransportResponseStatus Send(EventHarvestData eventHarvestData, IEnumerable<ErrorEventWireModel> errorEvents, string transactionId);
        DataTransportResponseStatus Send(EventHarvestData eventHarvestData, IEnumerable<ISpanEventWireModel> enumerable, string transactionId);
        DataTransportResponseStatus Send(IEnumerable<SqlTraceWireModel> sqlTraceWireModels, string transactionId);
        DataTransportResponseStatus Send(IEnumerable<CustomEventWireModel> customEvents, string transactionId);
        DataTransportResponseStatus Send(LogEventWireModelCollection loggingEvents, string transactionId);
        DataTransportResponseStatus Send(LoadedModuleWireModelCollection loadedModules, string transactionId);
    }

    public class DataTransportService : ConfigurationBasedService, IDataTransportService
    {
        private readonly IConnectionManager _connectionManager;
        private readonly IDateTimeStatic _dateTimeStatic;
        private DateTime _lastMetricSendTime;
        private readonly IAgentHealthReporter _agentHealthReporter;

        public DataTransportService(IConnectionManager connectionManager, IDateTimeStatic dateTimeStatic, IAgentHealthReporter agentHealthReporter)
        {
            _connectionManager = connectionManager;
            _dateTimeStatic = dateTimeStatic;
            _lastMetricSendTime = _dateTimeStatic.UtcNow;
            _agentHealthReporter = agentHealthReporter;
        }

        #region Public API

        public IEnumerable<CommandModel> GetAgentCommands()
        {
            var response = TrySendDataRequest<List<CommandModel>>("get_agent_commands", _configuration.AgentRunId);
            if (response.Status != DataTransportResponseStatus.RequestSuccessful || response.ReturnValue == null)
                return Enumerable.Empty<CommandModel>();

            return response.ReturnValue;
        }

        public void SendCommandResults(IDictionary<string, object> commandResults)
        {
            TrySendDataRequest("agent_command_results", _configuration.AgentRunId, commandResults);
        }

        public void SendThreadProfilingData(IEnumerable<ThreadProfilingModel> threadProfilingData)
        {
            TrySendDataRequest("profile_data", _configuration.AgentRunId, threadProfilingData);
        }

        public DataTransportResponseStatus Send(EventHarvestData eventHarvestData, IEnumerable<TransactionEventWireModel> transactionEvents, string transactionId)
        {
            return TrySendDataRequest("analytic_event_data", _configuration.AgentRunId, eventHarvestData, transactionEvents);
        }

        public DataTransportResponseStatus Send(EventHarvestData eventHarvestData, IEnumerable<ErrorEventWireModel> errorEvents, string transactionId)
        {
            return TrySendDataRequest("error_event_data", _configuration.AgentRunId, eventHarvestData, errorEvents);
        }

        public DataTransportResponseStatus Send(EventHarvestData eventHarvestData, IEnumerable<ISpanEventWireModel> spanEvents, string transactionId)
        {
            return TrySendDataRequest("span_event_data", _configuration.AgentRunId, eventHarvestData, spanEvents);
        }

        public DataTransportResponseStatus Send(IEnumerable<CustomEventWireModel> customEvents, string transactionId)
        {
            return TrySendDataRequest("custom_event_data", _configuration.AgentRunId, customEvents);
        }

        public DataTransportResponseStatus Send(IEnumerable<TransactionTraceWireModel> transactionSampleDatas, string transactionId)
        {
            _ = transactionId;
            return TrySendDataRequest("transaction_sample_data", _configuration.AgentRunId, transactionSampleDatas);
        }

        public DataTransportResponseStatus Send(IEnumerable<ErrorTraceWireModel> errorTraceDatas, string transactionId)
        {
            return TrySendDataRequest("error_data", _configuration.AgentRunId, errorTraceDatas);
        }

        public DataTransportResponseStatus Send(IEnumerable<SqlTraceWireModel> sqlTraceWireModels, string transactionId)
        {
            return TrySendDataRequest("sql_trace_data", sqlTraceWireModels);
        }

        public DataTransportResponseStatus Send(LogEventWireModelCollection loggingEvents, string transactionId)
        {
            return TrySendDataRequest("log_event_data", loggingEvents);
        }

        public DataTransportResponseStatus Send(IEnumerable<MetricWireModel> metrics, string transactionId)
        {
            if (!metrics.Any())
            {
                return DataTransportResponseStatus.RequestSuccessful;
            }

            var beginTime = _lastMetricSendTime;
            var endTime = _dateTimeStatic.UtcNow;
            if (beginTime >= endTime)
            {
                Log.Error("The last data send timestamp ({0}) is greater than or equal to the current timestamp ({1}). The metrics in this batch will be dropped.", _lastMetricSendTime, endTime);
                _lastMetricSendTime = _dateTimeStatic.UtcNow;
                return DataTransportResponseStatus.Discard;
            }

            var model = new MetricWireModelCollection(_configuration.AgentRunId as string, beginTime.ToUnixTimeSeconds(), endTime.ToUnixTimeSeconds(), metrics);

            var status = TrySendDataRequest("metric_data", model);

            if (status == DataTransportResponseStatus.RequestSuccessful)
                _lastMetricSendTime = endTime;

            return status;
        }

        public DataTransportResponseStatus Send(LoadedModuleWireModelCollection loadedModules, string transactionId)
        {
            if (loadedModules.LoadedModules.Count < 1)
            {
                return DataTransportResponseStatus.RequestSuccessful;
            }

            return TrySendDataRequest("update_loaded_modules", loadedModules);
        }

        #endregion Public API

        #region Private helpers


        private DataTransportResponseStatus TrySendDataRequest(string method, params object[] data)
        {
            var response = TrySendDataRequest<object>(method, data);
            return response.Status;
        }

        private DataTransportResponse<T> TrySendDataRequest<T>(string method, params object[] data)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var returnValue = _connectionManager.SendDataRequest<T>(method, data);
                return new DataTransportResponse<T>(DataTransportResponseStatus.RequestSuccessful, returnValue);
            }
            catch (HttpException ex)
            {
                LogErrorResponse(ex, method, startTime, ex.StatusCode);
                RestartOrShutdownIfNecessary(ex);
                var errorStatus = GetDataTransportResponseStatusByHttpStatusCode(ex.StatusCode);
                return new DataTransportResponse<T>(errorStatus);
            }
            catch (Exception ex) when (ex is SocketException || ex is WebException || ex is OperationCanceledException)
            {
                LogErrorResponse(ex, method, startTime, null);
                return new DataTransportResponse<T>(DataTransportResponseStatus.Retain);
            }
            catch (Exception ex)
            {
                LogErrorResponse(ex, method, startTime, null);
                return new DataTransportResponse<T>(DataTransportResponseStatus.Discard);
            }
        }

        private static void RestartOrShutdownIfNecessary(HttpException ex)
        {
            switch (ex.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.Conflict:
                    Restart();
                    break;
                case HttpStatusCode.Gone:
                    Shutdown(ex.Message);
                    break;
            }
        }

        private static void Shutdown(string message)
        {
            Log.Info("Shutting down: {0}", message);
            EventBus<KillAgentEvent>.Publish(new KillAgentEvent());
        }

        private static void Restart()
        {
            if(!Agent.IsAgentShuttingDown)
            {
                EventBus<RestartAgentEvent>.Publish(new RestartAgentEvent());
            }
            else
            {
                Log.Info("Agent was requested to restart, ignoring because shutdown is already in progress.");
            }
            
        }

        private void LogErrorResponse(Exception exception, string method, DateTime startTime, HttpStatusCode? httpStatusCode)
        {
            var endTime = DateTime.UtcNow;
            _agentHealthReporter.ReportSupportabilityCollectorErrorException(method, endTime - startTime, httpStatusCode);
            Log.Error(exception, "");
        }

        private DataTransportResponseStatus GetDataTransportResponseStatusByHttpStatusCode(HttpStatusCode httpStatusCode)
        {
            switch (httpStatusCode)
            {
                /* 400 */
                case HttpStatusCode.BadRequest:
                /* 401 */
                case HttpStatusCode.Unauthorized:
                /* 403 */
                case HttpStatusCode.Forbidden:
                /* 404 */
                case HttpStatusCode.NotFound:
                /* 405 */
                case HttpStatusCode.MethodNotAllowed:
                /* 407 */
                case HttpStatusCode.ProxyAuthenticationRequired:
                /* 409 */
                case HttpStatusCode.Conflict:
                /* 410 */
                case HttpStatusCode.Gone:
                /* 411 */
                case HttpStatusCode.LengthRequired:
                /* 414 */
                case HttpStatusCode.RequestUriTooLong:
                /* 415 */
                case HttpStatusCode.UnsupportedMediaType:
                /* 417 */
                case HttpStatusCode.ExpectationFailed:
                /* 431 */
                case (HttpStatusCode)431:
                    return DataTransportResponseStatus.Discard;

                /* 408 */
                case HttpStatusCode.RequestTimeout:
                /* 429 */
                case (HttpStatusCode)429:
                /* 500 */
                case HttpStatusCode.InternalServerError:
                /* 503 */
                case HttpStatusCode.ServiceUnavailable:
                    return DataTransportResponseStatus.Retain;

                /* 413 */
                case HttpStatusCode.RequestEntityTooLarge:
                    return DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard;

                default:
                    return DataTransportResponseStatus.Discard;
            }
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
