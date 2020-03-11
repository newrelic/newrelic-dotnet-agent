using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core;
using NewRelic.Core.DistributedTracing;
using NewRelic.Core.Logging;
using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.DistributedTracing
{
	enum DistributedTraceHeaderType
	{
		NewRelic,
		W3cTraceparent,
		W3cTracestate
	}

	public interface IDistributedTracePayloadHandler
	{
		IDistributedTracePayload TryGetOutboundDistributedTraceApiModel(IInternalTransaction transaction, ISegment segment);

		DistributedTracePayload TryDecodeInboundSerializedDistributedTracePayload(string serializedPayload);

		ITracingState AcceptDistributedTracePayload(string serializedPayload, TransportType transportType);

		ITracingState AcceptDistributedTraceHeaders(Func<string, IList<string>> getHeaders, TransportType transportType);

		void InsertDistributedTraceHeaders(IInternalTransaction transaction, Action<string, string> setHeaders);
	}

	public class DistributedTracePayloadHandler : IDistributedTracePayloadHandler
	{
		private const string DistributedTraceTypeDefault = "App";
		private const int ParentType = 0; 
		private const string TraceParentVersion = "00";
		private const string PriorityFormat = "{0:0.######}";
		private const int TraceIdMaxLength = 32;
		private readonly IConfigurationService _configurationService;
		private readonly IAgentHealthReporter _agentHealthReporter;
		private readonly IAdaptiveSampler _adaptiveSampler;

		public DistributedTracePayloadHandler(IConfigurationService configurationService, IAgentHealthReporter agentHealthReporter, IAdaptiveSampler adaptiveSampler)
		{
			_configurationService = configurationService;
			_agentHealthReporter = agentHealthReporter;
			_adaptiveSampler = adaptiveSampler;
		}

		#region Outgoing/Create

		public void InsertDistributedTraceHeaders(IInternalTransaction transaction, Action<string, string> setHeaders) 
		{
			if(setHeaders == null) 
			{
				Log.Debug("setHeaders argument is null.");
				return;
			}

			try
			{

				var timestamp = DateTime.UtcNow;
				if (!_configurationService.Configuration.ExcludeNewrelicHeader)
				{
					//set "Newrelic" header
					var distributedTracePayload = GetOutboundHeader(DistributedTraceHeaderType.NewRelic, transaction, timestamp);
					setHeaders(Constants.DistributedTracePayloadKey, distributedTracePayload);
				}

				var traceparent = GetOutboundHeader(DistributedTraceHeaderType.W3cTraceparent, transaction, timestamp);
				var tracestate = GetOutboundHeader(DistributedTraceHeaderType.W3cTracestate, transaction, timestamp);

				setHeaders(Constants.TraceParentHeaderKey, traceparent);
				setHeaders(Constants.TraceStateHeaderKey, tracestate);
			}
			catch(Exception ex) 
			{
				Log.Error(ex);
			}
		}

		private string GetOutboundHeader(DistributedTraceHeaderType headerType, IInternalTransaction transaction, DateTime timestamp)
		{
			switch (headerType) 
			{
				case DistributedTraceHeaderType.W3cTraceparent:
					return BuildTraceParent(transaction);
				case DistributedTraceHeaderType.W3cTracestate:
					return BuildTracestate(transaction, timestamp);
				case DistributedTraceHeaderType.NewRelic:
					var distrubtedTracePayload = TryGetOutboundDistributedTraceApiModelInternal(transaction, transaction.CurrentSegment, timestamp);
					if (!distrubtedTracePayload.IsEmpty())
					{
						return distrubtedTracePayload.HttpSafe();
					}
					return string.Empty;
			}

			return string.Empty;
		}

		private string BuildTraceParent(IInternalTransaction transaction)
		{
			var traceId = transaction.TraceId ?? transaction.Guid;
			traceId = FormatTraceId(traceId);
			var parentId = _configurationService.Configuration.SpanEventsEnabled ? transaction.CurrentSegment.SpanId : GuidGenerator.GenerateNewRelicTraceId();
			var flags = transaction.Sampled.Value ? "01" : "00";
			return $"{TraceParentVersion}-{traceId}-{parentId}-{flags}";
		}

		private string FormatTraceId(string traceId)
		{
			traceId = traceId.ToLowerInvariant();
			if (traceId.Length < 32)
			{
				traceId = traceId.PadLeft(TraceIdMaxLength, '0');
			}

			return traceId;
		}

		private string BuildTracestate(IInternalTransaction transaction, DateTime timestamp)
		{
			var trustedAccountKey = _configurationService.Configuration.TrustedAccountKey;
			var version = 0;
			var parentType = ParentType;
			var accountId = _configurationService.Configuration.AccountId;
			var appId = _configurationService.Configuration.PrimaryApplicationId;
			var spanId = _configurationService.Configuration.SpanEventsEnabled ? transaction.CurrentSegment.SpanId : string.Empty;
			var transactionId = _configurationService.Configuration.TransactionEventsEnabled ? transaction.Guid : string.Empty;
			var sampled = transaction.Sampled.Value ? "1" : "0";
			var priority = string.Format(PriorityFormat, transaction.Priority);
			var timestampInMillis = timestamp.ToUnixTimeMilliseconds();

			var newRelicTracestate = $"{trustedAccountKey}@nr={version}-{parentType}-{accountId}-{appId}-{spanId}-{transactionId}-{sampled}-{priority}-{timestampInMillis}";
			var otherVendorTracestates = string.Empty;

			if (transaction.TracingState != null)
			{
				if (transaction.TracingState.VendorStateEntries != null)
				{
					otherVendorTracestates = string.Join(",", transaction.TracingState.VendorStateEntries);
				}
			}

			return string.Join(",", newRelicTracestate, otherVendorTracestates);
		}

		public IDistributedTracePayload TryGetOutboundDistributedTraceApiModel(IInternalTransaction transaction, ISegment segment = null)
		{
			return TryGetOutboundDistributedTraceApiModelInternal(transaction, segment, DateTime.UtcNow);
		}

		private IDistributedTracePayload TryGetOutboundDistributedTraceApiModelInternal(IInternalTransaction transaction, ISegment segment, DateTime timestamp)
		{
			var accountId = _configurationService.Configuration.AccountId;
			var appId = _configurationService.Configuration.PrimaryApplicationId;

			if (!_configurationService.Configuration.SpanEventsEnabled && !_configurationService.Configuration.TransactionEventsEnabled)
			{
				Log.Finest("Did not generate payload because Span Events and Transaction Events were both disabled, preventing a traceable payload.");
				return DistributedTraceApiModel.EmptyModel;
			}

			transaction.SetSampled(_adaptiveSampler);
			var transactionIsSampled = transaction.Sampled;

			if (transactionIsSampled.HasValue == false)
			{
				Log.Error("Did not generate payload because transaction sampled value was null.");
				return DistributedTraceApiModel.EmptyModel;
			}

			var payloadGuid = (_configurationService.Configuration.SpanEventsEnabled && transactionIsSampled.Value) ? segment?.SpanId : null;
			var trustKey = _configurationService.Configuration.TrustedAccountKey;
			var transactionId = (_configurationService.Configuration.TransactionEventsEnabled) ? transaction.Guid : null;
			var traceId = transaction.TraceId ?? transaction.Guid;

			var distributedTracePayload = DistributedTracePayload.TryBuildOutgoingPayload(
				DistributedTraceTypeDefault,
				accountId,
				appId,
				payloadGuid,
				traceId,
				trustKey,
				transaction.Priority,
				transactionIsSampled,
				timestamp,
				transactionId);

			if (distributedTracePayload == null)
			{
				return DistributedTraceApiModel.EmptyModel;
			}

			string encodedPayload;

			try
			{
				encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(distributedTracePayload);
			}
			catch (Exception ex)
			{
				Log.Error($"Failed to get encoded distributed trace headers for outbound request: {ex}");
				_agentHealthReporter.ReportSupportabilityDistributedTraceCreatePayloadException();
				return DistributedTraceApiModel.EmptyModel;
			}

			transaction.TransactionMetadata.HasOutgoingDistributedTracePayload = true;

			if (_configurationService.Configuration.PayloadSuccessMetricsEnabled)
			{
				_agentHealthReporter.ReportSupportabilityDistributedTraceCreatePayloadSuccess();
			}

			return new DistributedTraceApiModel(encodedPayload);
		}

		#endregion Outgoing/Create

		#region Incoming/Accept

		public ITracingState AcceptDistributedTracePayload(string serializedPayload, TransportType transportType)
		{
			var tracingState = TracingState.AcceptDistributedTracePayload(serializedPayload, transportType, _configurationService.Configuration.TrustedAccountKey);

			if (tracingState.IngestErrors != null)
			{
				ReportIncomingErrors(tracingState.IngestErrors);
				return null;
			}
			else
			{
				if (_configurationService.Configuration.PayloadSuccessMetricsEnabled)
				{
					_agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadSuccess();
				}
			}

			return tracingState;
		}

		public ITracingState AcceptDistributedTraceHeaders(Func<string, IList<string>> getHeaders, TransportType transportType)
		{
			if (getHeaders == null)
			{
				Log.Debug("getHeaders argument is null.");
				return null;
			}

			try
			{
				var tracingState = TracingState.AcceptDistributedTraceHeaders(getHeaders, transportType, _configurationService.Configuration.TrustedAccountKey);
				return tracingState;
			} catch(Exception ex) 
			{
				Log.Error(ex);
				return null;
			}
		}

		private void ReportIncomingErrors(List<IngestErrorType> errors)
		{
			foreach (IngestErrorType error in errors)
			{
				switch(error)
				{
					case IngestErrorType.Version:
						_agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredMajorVersion();
						break;

					case IngestErrorType.NullPayload:
						_agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredNull();
						break;

					case IngestErrorType.ParseException:
						_agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadParseException();
						break;

					case IngestErrorType.OtherException:
						_agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadException();
						break;

					case IngestErrorType.NotTraceable:
						Log.Debug("Incoming Guid and TransactionId were null, which is invalid for a Distributed Trace payload.");
						_agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadParseException();
						break;

					case IngestErrorType.NotTrusted:
						Log.Debug($"Incoming trustKey or accountId not trusted, distributed trace payload will be ignored.");
						_agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredUntrustedAccount();
						break;

					default:
						break;
				}
			}
		}

		public DistributedTracePayload TryDecodeInboundSerializedDistributedTracePayload(string serializedPayload)
		{
			DistributedTracePayload payload = null;

			try
			{
				payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(serializedPayload);
			}
			catch (DistributedTraceAcceptPayloadVersionException)
			{
				_agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredMajorVersion();
			}
			catch (DistributedTraceAcceptPayloadNullException)
			{
				_agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredNull();
			}
			catch (DistributedTraceAcceptPayloadParseException)
			{
				_agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadParseException();
			}
			catch (Exception)
			{
				_agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadException();
			}

			if (!IsValidPayload(payload))
			{
				return null;
			}

			if (_configurationService.Configuration.PayloadSuccessMetricsEnabled)
			{
				_agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadSuccess();
			}

			return payload;
		}

		private bool IsValidPayload(DistributedTracePayload payload)
		{
			if (payload == null)
			{
				return false;
			}

			return HasTraceablePayload(payload) && HasTrustedAccountKey(payload);
		}

		private bool HasTraceablePayload(DistributedTracePayload payload)
		{
			if ((payload.Guid == null) && (payload.TransactionId == null))
			{
				Log.Debug("Incoming Guid and TransactionId were null, which is invalid for a Distributed Trace payload.");
				_agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadParseException();

				return false;
			}

			return true;
		}

		private bool HasTrustedAccountKey(DistributedTracePayload payload)
		{
			var incomingTrustKey = payload.TrustKey ?? payload.AccountId;
			var isTrusted = incomingTrustKey == _configurationService.Configuration.TrustedAccountKey;
			
			if (!isTrusted)
			{
				Log.Debug($"Incoming trustKey or accountId [{incomingTrustKey}] not trusted, distributed trace payload will be ignored.");
				_agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredUntrustedAccount();
			}

			return isTrusted;
		}

		#endregion Incoming/Accept
	}
}
