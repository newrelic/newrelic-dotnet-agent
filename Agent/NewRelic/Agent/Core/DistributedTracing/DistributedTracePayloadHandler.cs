using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.DistributedTracing
{
	public interface IDistributedTracePayloadHandler
	{
		IDistributedTraceApiModel TryGetOutboundDistributedTraceApiModel(ITransaction transaction, ISegment segment);

		DistributedTracePayload TryDecodeInboundSerializedDistributedTracePayload(string serializedPayload);
	}

	public class DistributedTracePayloadHandler : IDistributedTracePayloadHandler
	{
		private const string DistributedTraceTypeDefault = "App";

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

		public IDistributedTraceApiModel TryGetOutboundDistributedTraceApiModel(ITransaction transaction, ISegment segment = null)
		{
			var accountId = _configurationService.Configuration.AccountId;
			var appId = _configurationService.Configuration.PrimaryApplicationId;

			if (!_configurationService.Configuration.SpanEventsEnabled && !_configurationService.Configuration.TransactionEventsEnabled)
			{ 
				Log.Finest("Did not generate payload because Span Events and Transaction Events were both disabled, preventing a traceable payload.");
				return DistributedTraceApiModel.EmptyModel;
			}

			var transactionMetadata = transaction.TransactionMetadata;
			transactionMetadata.SetSampled(_adaptiveSampler);
			var transactionIsSampled = transactionMetadata.DistributedTraceSampled;

			if (transactionIsSampled.HasValue == false)
			{
				Log.Error("Did not generate payload because transaction sampled value was null.");
				return DistributedTraceApiModel.EmptyModel;
			}

			var payloadGuid = (_configurationService.Configuration.SpanEventsEnabled && transactionIsSampled.Value) ? segment?.SpanId : null;
			var trustKey = _configurationService.Configuration.TrustedAccountKey;
			var transactionId = (_configurationService.Configuration.TransactionEventsEnabled) ? transaction.Guid : null;
			var traceId = transactionMetadata.DistributedTraceTraceId ?? transaction.Guid;

			var distributedTracePayload = DistributedTracePayload.TryBuildOutgoingPayload(
				DistributedTraceTypeDefault,
				accountId,
				appId,
				payloadGuid,
				traceId,
				trustKey,
				transactionMetadata.Priority,
				transactionIsSampled,
				DateTime.UtcNow,
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

			transactionMetadata.HasOutgoingDistributedTracePayload = true;

			if (_configurationService.Configuration.PayloadSuccessMetricsEnabled)
			{
				_agentHealthReporter.ReportSupportabilityDistributedTraceCreatePayloadSuccess();
			}

			return new DistributedTraceApiModel(encodedPayload);
		}

		#endregion Outgoing/Create

		#region Incoming/Accept

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
