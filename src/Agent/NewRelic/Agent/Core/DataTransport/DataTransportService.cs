// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Commands;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Core.SharedInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface IDataTransportService
    {
        Task<IEnumerable<CommandModel>> GetAgentCommandsAsync();
        Task SendCommandResultsAsync(IDictionary<string, object> commandResults);
        Task SendThreadProfilingDataAsync(IEnumerable<ThreadProfilingModel> threadProfilingData);
        Task<DataTransportResponseStatus> SendAsync(IEnumerable<TransactionTraceWireModel> transactionSampleDatas,
            string transactionId);
        Task<DataTransportResponseStatus> SendAsync(IEnumerable<ErrorTraceWireModel> errorTraceDatas, string transactionId);
        Task<DataTransportResponseStatus> SendAsync(IEnumerable<MetricWireModel> metrics, string transactionId);
        Task<DataTransportResponseStatus> SendAsync(EventHarvestData eventHarvestData,
            IEnumerable<TransactionEventWireModel> transactionEvents, string transactionId);
        Task<DataTransportResponseStatus> SendAsync(EventHarvestData eventHarvestData,
            IEnumerable<ErrorEventWireModel> errorEvents, string transactionId);
        Task<DataTransportResponseStatus> SendAsync(EventHarvestData eventHarvestData,
            IEnumerable<ISpanEventWireModel> enumerable, string transactionId);
        Task<DataTransportResponseStatus> SendAsync(IEnumerable<SqlTraceWireModel> sqlTraceWireModels, string transactionId);
        Task<DataTransportResponseStatus> SendAsync(IEnumerable<CustomEventWireModel> customEvents, string transactionId);
        Task<DataTransportResponseStatus> SendAsync(LogEventWireModelCollection loggingEvents, string transactionId);
        Task<DataTransportResponseStatus> SendAsync(LoadedModuleWireModelCollection loadedModules, string transactionId);
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

        public async Task<IEnumerable<CommandModel>> GetAgentCommandsAsync()
        {
            var response = await TrySendDataRequestAsync<List<CommandModel>>("get_agent_commands", _configuration.AgentRunId).ConfigureAwait(false);
            if (response.Status != DataTransportResponseStatus.RequestSuccessful || response.ReturnValue == null)
                return Enumerable.Empty<CommandModel>();

            return response.ReturnValue;
        }

        public async Task SendCommandResultsAsync(IDictionary<string, object> commandResults)
        {
            await TrySendDataRequestAsync("agent_command_results", _configuration.AgentRunId, commandResults).ConfigureAwait(false);
        }

        public async Task SendThreadProfilingDataAsync(IEnumerable<ThreadProfilingModel> threadProfilingData)
        {
            await TrySendDataRequestAsync("profile_data", _configuration.AgentRunId, threadProfilingData).ConfigureAwait(false);
        }

        public async Task<DataTransportResponseStatus> SendAsync(EventHarvestData eventHarvestData, IEnumerable<TransactionEventWireModel> transactionEvents, string transactionId)
        {
            return await TrySendDataRequestAsync("analytic_event_data", _configuration.AgentRunId, eventHarvestData, transactionEvents).ConfigureAwait(false);
        }

        public async Task<DataTransportResponseStatus> SendAsync(EventHarvestData eventHarvestData, IEnumerable<ErrorEventWireModel> errorEvents, string transactionId)
        {
            return await TrySendDataRequestAsync("error_event_data", _configuration.AgentRunId, eventHarvestData, errorEvents).ConfigureAwait(false);
        }

        public async Task<DataTransportResponseStatus> SendAsync(EventHarvestData eventHarvestData, IEnumerable<ISpanEventWireModel> spanEvents, string transactionId)
        {
            return await TrySendDataRequestAsync("span_event_data", _configuration.AgentRunId, eventHarvestData, spanEvents).ConfigureAwait(false);
        }

        public async Task<DataTransportResponseStatus> SendAsync(IEnumerable<CustomEventWireModel> customEvents, string transactionId)
        {
            return await TrySendDataRequestAsync("custom_event_data", _configuration.AgentRunId, customEvents).ConfigureAwait(false);
        }

        public async Task<DataTransportResponseStatus> SendAsync(IEnumerable<TransactionTraceWireModel> transactionSampleDatas, string transactionId)
        {
            _ = transactionId;
            return await TrySendDataRequestAsync("transaction_sample_data", _configuration.AgentRunId, transactionSampleDatas).ConfigureAwait(false);
        }

        public async Task<DataTransportResponseStatus> SendAsync(IEnumerable<ErrorTraceWireModel> errorTraceDatas, string transactionId)
        {
            return await TrySendDataRequestAsync("error_data", _configuration.AgentRunId, errorTraceDatas).ConfigureAwait(false);
        }

        public async Task<DataTransportResponseStatus> SendAsync(IEnumerable<SqlTraceWireModel> sqlTraceWireModels, string transactionId)
        {
            return await TrySendDataRequestAsync("sql_trace_data", sqlTraceWireModels).ConfigureAwait(false);
        }

        public async Task<DataTransportResponseStatus> SendAsync(LogEventWireModelCollection loggingEvents, string transactionId)
        {
            return await TrySendDataRequestAsync("log_event_data", loggingEvents).ConfigureAwait(false);
        }

        public async Task<DataTransportResponseStatus> SendAsync(IEnumerable<MetricWireModel> metrics, string transactionId)
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

            var status = await TrySendDataRequestAsync("metric_data", model).ConfigureAwait(false);

            if (status == DataTransportResponseStatus.RequestSuccessful)
                _lastMetricSendTime = endTime;

            return status;
        }

        public async Task<DataTransportResponseStatus> SendAsync(LoadedModuleWireModelCollection loadedModules, string transactionId)
        {
            if (loadedModules.LoadedModules.Count < 1)
            {
                return DataTransportResponseStatus.RequestSuccessful;
            }

            return await TrySendDataRequestAsync("update_loaded_modules", loadedModules).ConfigureAwait(false);
        }

        #endregion Public API

        #region Private helpers


        private async Task<DataTransportResponseStatus> TrySendDataRequestAsync(string method, params object[] data)
        {
            var response = await TrySendDataRequestAsync<object>(method, data).ConfigureAwait(false);
            return response.Status;
        }

        private async Task<DataTransportResponse<T>> TrySendDataRequestAsync<T>(string method, params object[] data)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var returnValue = await _connectionManager.SendDataRequestAsync<T>(method, data).ConfigureAwait(false);
                return new DataTransportResponse<T>(DataTransportResponseStatus.RequestSuccessful, returnValue);
            }
            catch (HttpException ex)
            {
                LogErrorResponse(ex, method, startTime, ex.StatusCode);
                RestartOrShutdownIfNecessary(ex);
                var errorStatus = GetDataTransportResponseStatusByHttpStatusCode(ex.StatusCode);
                return new DataTransportResponse<T>(errorStatus);
            }
            // OperationCanceledException is a base class for TaskCanceledException, which can be thrown by HttpClient.SendAsync in .NET 6+
            catch (Exception ex) when (ex is SocketException or WebException or OperationCanceledException) 
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
            EventBus<KillAgentEvent>.PublishAsync(new KillAgentEvent());
        }

        private static void Restart()
        {
            if(!Agent.IsAgentShuttingDown)
            {
                EventBus<RestartAgentEvent>.PublishAsync(new RestartAgentEvent());
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
            Log.Error(exception, "Exception in TrySendDataRequest():");
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
