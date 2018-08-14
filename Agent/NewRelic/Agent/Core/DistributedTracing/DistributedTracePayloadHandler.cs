using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using ProvidersWrapper = NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.DistributedTracing
{
	public interface IDistributedTracePayloadHandler
	{
		[NotNull]
		IEnumerable<KeyValuePair<string, string>> TryGetOutboundRequestHeaders([NotNull] ITransaction transaction, ProvidersWrapper.ISegment segment);

		[CanBeNull]
		DistributedTracePayload TryDecodeInboundRequestHeaders([NotNull] IEnumerable<KeyValuePair<string, string>> headers);
	}

	public class DistributedTracePayloadHandler : IDistributedTracePayloadHandler
	{
		private const string DistributedTraceHeaderName = "Newrelic";   // betterCAT v0.2

		private const string DistributedTraceTypeDefault = "App";

		[NotNull]
		private readonly IConfigurationService _configurationService;
		private readonly IAgentHealthReporter _agentHealthReporter;
		private readonly IAdaptiveSampler _adaptiveSampler;

		public DistributedTracePayloadHandler([NotNull] IConfigurationService configurationService, IAgentHealthReporter agentHealthReporter, IAdaptiveSampler adaptiveSampler)
		{
			_configurationService = configurationService;
			_agentHealthReporter = agentHealthReporter;
			_adaptiveSampler = adaptiveSampler;
		}

		#region Outgoing/Create

		public IEnumerable<KeyValuePair<string, string>> TryGetOutboundRequestHeaders(ITransaction transaction, ProvidersWrapper.ISegment segment = null)
		{
			var accountId = _configurationService.Configuration.AccountId;
			var appId = _configurationService.Configuration.PrimaryApplicationId;

			if (!_configurationService.Configuration.SpanEventsEnabled && !_configurationService.Configuration.TransactionEventsEnabled)
			{ 
				Log.Finest("Did not generate payload because Span Events and Transaction Events were both disabled, preventing a traceable payload.");
				return Enumerable.Empty<KeyValuePair<string, string>>();
			}

			var transactionMetadata = transaction.TransactionMetadata;
			transactionMetadata.SetSampled(_adaptiveSampler);
			var transactionIsSampled = transactionMetadata.DistributedTraceSampled;

			if (transactionIsSampled.HasValue == false)
			{
				Log.Error("Did not generate payload because transaction sampled value was null.");
				return Enumerable.Empty<KeyValuePair<string, string>>();
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
				return Enumerable.Empty<KeyValuePair<string, string>>();
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
				return Enumerable.Empty<KeyValuePair<string, string>>();
			}

			transactionMetadata.HasOutgoingDistributedTracePayload = true;

			_agentHealthReporter.ReportSupportabilityDistributedTraceCreatePayloadSuccess();

			return new Dictionary<string, string>
			{
				{ DistributedTraceHeaderName, encodedPayload }
			};
		}

		#endregion Outgoing/Create

		#region Incoming/Accept

		public DistributedTracePayload TryDecodeInboundRequestHeaders(IEnumerable<KeyValuePair<string, string>> headers)
		{
			DistributedTracePayload payload = null;

			try
			{
				var distributedTraceHeader = TryGetDistributedTraceHeader(headers);
				if (distributedTraceHeader == null)
				{
					return null;
				}

				payload = HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload(distributedTraceHeader);
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

			_agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadSuccess();
			return payload;
		}

		private static string TryGetDistributedTraceHeader(IEnumerable<KeyValuePair<string, string>> headers)
		{
			foreach (var keyValuePair in headers)
			{
				if (string.Compare(DistributedTraceHeaderName, keyValuePair.Key, StringComparison.OrdinalIgnoreCase) == 0)
				{
					return keyValuePair.Value ?? throw new DistributedTraceAcceptPayloadNullException();
				}
			}

			return null;
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
