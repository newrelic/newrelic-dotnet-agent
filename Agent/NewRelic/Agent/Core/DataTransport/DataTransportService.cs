using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using JetBrains.Annotations;
using NewRelic.Agent.Core.AgentHealth;
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
		[NotNull]
		private readonly IConnectionManager _connectionManager;

		[NotNull]
		private readonly IDateTimeStatic _dateTimeStatic;

		private DateTime _lastMetricSendTime;

		private readonly IAgentHealthReporter _agentHealthReporter;

		public DataTransportService([NotNull] IConnectionManager connectionManager, [NotNull] IDateTimeStatic dateTimeStatic, [NotNull] IAgentHealthReporter agentHealthReporter)
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

		public DataTransportResponseStatus Send(EventHarvestData eventHarvestData, IEnumerable<ErrorEventWireModel> errorEvents)
		{
			var response = TrySendDataRequest("error_event_data", _configuration.AgentRunId, eventHarvestData, errorEvents);
			return response.Status;
		}

		public DataTransportResponseStatus Send(EventHarvestData eventHarvestData, IEnumerable<SpanEventWireModel> spanEvents)
		{
			var response = TrySendDataRequest("span_event_data", _configuration.AgentRunId, eventHarvestData, spanEvents);
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

			// TODO: clean this up (requires enveloping to be moved to a different layer)
			var beginTime = _lastMetricSendTime;
			var endTime = _dateTimeStatic.UtcNow;
			if (beginTime >= endTime)
			{
				Log.ErrorFormat("The last data send timestamp ({0}) is greater than or equal to the current timestamp ({1}). The metrics in this batch will be dropped.", _lastMetricSendTime, endTime);
				_lastMetricSendTime = _dateTimeStatic.UtcNow;
				return DataTransportResponseStatus.OtherError;
			}

			var response = TrySendDataRequest("metric_data", _configuration.AgentRunId, beginTime.ToUnixTime(), endTime.ToUnixTime(), metrics);

			if (response.Status == DataTransportResponseStatus.RequestSuccessful)
				_lastMetricSendTime = endTime;

			return response.Status;
		}

		#endregion Public API

		#region Private helpers


		private DataTransportResponse<Object> TrySendDataRequest([NotNull] String method, [NotNull] params Object[] data)
		{
			return TrySendDataRequest<Object>(method, data);
		}

		private DataTransportResponse<T> TrySendDataRequest<T>([NotNull] String method, [NotNull] params Object[] data)
		{
			var startTime = DateTime.UtcNow;
			try
			{
				var returnValue = _connectionManager.SendDataRequest<T>(method, data);
				return new DataTransportResponse<T>(DataTransportResponseStatus.RequestSuccessful, returnValue);
			}
			catch (LicenseException ex)
			{
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
				return GetErrorResponse<T>(ex, DataTransportResponseStatus.ConnectionError, method, startTime);
			}
			catch (PostTooBigException ex)
			{
				return GetErrorResponse<T>(ex, DataTransportResponseStatus.PostTooBigError, method, startTime);
			}
			catch (PostTooLargeException ex)
			{
				return GetErrorResponse<T>(ex, DataTransportResponseStatus.PostTooBigError, method, startTime, ex.StatusCode);
			}
			catch (RequestTimeoutException ex)
			{
				return GetErrorResponse<T>(ex, DataTransportResponseStatus.RequestTimeout, method, startTime, ex.StatusCode);
			}
			catch (ServerErrorException ex)
			{
				return GetErrorResponse<T>(ex, DataTransportResponseStatus.ServerError, method, startTime, ex.StatusCode);
			}
			catch (SocketException ex)
			{
				return GetErrorResponse<T>(ex, DataTransportResponseStatus.CommunicationError, method, startTime);
			}
			catch (WebException ex)
			{
				return GetErrorResponse<T>(ex, DataTransportResponseStatus.CommunicationError, method, startTime);
			}
			catch (Exception ex)
			{
				return GetErrorResponse<T>(ex, DataTransportResponseStatus.OtherError, method, startTime);
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

		private DataTransportResponse<T> GetErrorResponse<T>([NotNull] Exception exception, DataTransportResponseStatus errorStatus, string method, DateTime startTime, HttpStatusCode? httpStatusCode = null)
		{
			var endTime = DateTime.UtcNow;
			_agentHealthReporter.ReportSupportabilityCollectorErrorException(method, endTime - startTime, httpStatusCode);
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
