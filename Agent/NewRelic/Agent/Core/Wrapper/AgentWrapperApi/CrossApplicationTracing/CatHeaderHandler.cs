using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Helpers;
using NewRelic.Core;
using NewRelic.Core.Logging;
using NewRelic.SystemExtensions.Collections.Generic;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing
{
	public interface ICatHeaderHandler
	{
		IEnumerable<KeyValuePair<string, string>> TryGetOutboundRequestHeaders(IInternalTransaction transaction);
		IEnumerable<KeyValuePair<string, string>> TryGetOutboundResponseHeaders(IInternalTransaction transaction, TransactionMetricName transactionMetricName);
		CrossApplicationResponseData TryDecodeInboundResponseHeaders(IDictionary<string, string> headers);
		string TryDecodeInboundRequestHeadersForCrossProcessId(IDictionary<string, string> headers);
		CrossApplicationRequestData TryDecodeInboundRequestHeaders(IDictionary<string, string> headers);
	}

	public class CatHeaderHandler : ICatHeaderHandler
	{
		private const string NewRelicIdHttpHeader = "X-NewRelic-ID";
		private const string TransactionDataHttpHeader = "X-NewRelic-Transaction";
		private const string AppDataHttpHeader = "X-NewRelic-App-Data";


		private readonly IConfigurationService _configurationService;

		public CatHeaderHandler(IConfigurationService configurationService)
		{
			_configurationService = configurationService;
		}

		public IEnumerable<KeyValuePair<string, string>> TryGetOutboundRequestHeaders(IInternalTransaction transaction)
		{
			try
			{
				if (!_configurationService.Configuration.CrossApplicationTracingEnabled)
					return Enumerable.Empty<KeyValuePair<string, string>>();

				var crossProcessId = _configurationService.Configuration.CrossApplicationTracingCrossProcessId;
				if (crossProcessId == null)
				{
					Log.Error("Failed to get cross process id for outbound request.");
					return Enumerable.Empty<KeyValuePair<string, string>>();
				}

				var encodedNewRelicId = GetEncodedNewRelicId(crossProcessId);
				var encodedTransactionData = GetEncodedTransactionData(transaction);

				return new Dictionary<string, string>
				{
					{NewRelicIdHttpHeader, encodedNewRelicId},
					{TransactionDataHttpHeader, encodedTransactionData}
				};
			}
			catch (Exception ex)
			{
				Log.Error($"Failed to get encoded CAT headers for outbound request: {ex}");
				return Enumerable.Empty<KeyValuePair<string, string>>();
			}
		}

		public IEnumerable<KeyValuePair<string, string>> TryGetOutboundResponseHeaders(IInternalTransaction transaction, TransactionMetricName transactionMetricName)
		{
			try
			{
				if (!_configurationService.Configuration.CrossApplicationTracingEnabled)
					return Enumerable.Empty<KeyValuePair<string, string>>();

				var refereeCrossProcessId = _configurationService.Configuration.CrossApplicationTracingCrossProcessId;
				if (refereeCrossProcessId == null)
				{
					Log.Error("Failed to get cross process id for outbound response.");
					return Enumerable.Empty<KeyValuePair<string, string>>();
				}

				var encodedAppData = GetEncodedAppData(transaction, transactionMetricName, refereeCrossProcessId);

				return new Dictionary<string, string>
				{
					{AppDataHttpHeader, encodedAppData},
				};
			}
			catch (Exception ex)
			{
				Log.Error($"Failed to get encoded CAT headers for outbound response: {ex}");
				return Enumerable.Empty<KeyValuePair<string, string>>();
			}
		}

		public CrossApplicationResponseData TryDecodeInboundResponseHeaders(IDictionary<string, string> headers)
		{
			if (!_configurationService.Configuration.CrossApplicationTracingEnabled)
				return null;

			var responseHeader = headers.GetValueOrDefault(AppDataHttpHeader);
			if (responseHeader == null)
			{
				return null;
			}

			//It is possible that multiple instrumentations, on the service side, try to add New Relic header
			//to the response on the same transaction. When that happens, the response received by the client
			//has the New Relic header contains multiple header data separated by commas. The agent will only
			//decode the first header data in this case.
			var separatorIndex = responseHeader.IndexOf(",");
			if (separatorIndex > 0)
			{
				responseHeader = responseHeader.Substring(0, separatorIndex);
			}

			try
			{
				return HeaderEncoder.TryDecodeAndDeserialize <CrossApplicationResponseData>(responseHeader, _configurationService.Configuration.EncodingKey);
			}
			catch (Newtonsoft.Json.JsonSerializationException)
			{
				return null;
			}
		}

		public string TryDecodeInboundRequestHeadersForCrossProcessId(IDictionary<string, string> headers)
		{
			if (!_configurationService.Configuration.CrossApplicationTracingEnabled)
				return null;

			var encodedNewRelicIdHttpHeader = headers.GetValueOrDefault(NewRelicIdHttpHeader);
			if (encodedNewRelicIdHttpHeader == null)
				return null;

			var decodedCrossProcessId = TryDecodeNewRelicIdHttpHeader(encodedNewRelicIdHttpHeader);
			if (decodedCrossProcessId == null)
				return null;

			if (!IsTrustedCrossProcessAccountId(decodedCrossProcessId, _configurationService.Configuration.TrustedAccountIds))
				return null;

			return decodedCrossProcessId;
		}

		public CrossApplicationRequestData TryDecodeInboundRequestHeaders(IDictionary<string, string> headers)
		{
			if (!_configurationService.Configuration.CrossApplicationTracingEnabled)
			{
				return null;
			}

			var encodedTransactionDataHttpHeader = headers.GetValueOrDefault(TransactionDataHttpHeader);
			if (encodedTransactionDataHttpHeader == null)
			{
				return null;
			}

			var data = HeaderEncoder.TryDecodeAndDeserialize<CrossApplicationRequestData>(encodedTransactionDataHttpHeader, _configurationService.Configuration.EncodingKey);

			return data;
		}

		private string TryDecodeNewRelicIdHttpHeader(string encodedNewRelicIdHttpHeader)
		{
			if (encodedNewRelicIdHttpHeader == null)
				return null;

			return Strings.TryBase64Decode(encodedNewRelicIdHttpHeader, _configurationService.Configuration.EncodingKey);
		}

		private string GetEncodedAppData(IInternalTransaction transaction, TransactionMetricName transactionMetricName, string crossProcessId)
		{
			var txMetadata = transaction.TransactionMetadata;
			var queueTime = txMetadata.QueueTime?.TotalSeconds ?? 0;
			var referrerContentLength = txMetadata.GetCrossApplicationReferrerContentLength();
			var responseTimeInSeconds = txMetadata.CrossApplicationResponseTimeInSeconds;
			var appData = new CrossApplicationResponseData(crossProcessId, transactionMetricName.PrefixedName, (float)queueTime, responseTimeInSeconds, referrerContentLength, transaction.Guid);

			return HeaderEncoder.EncodeSerializedData(JsonConvert.SerializeObject(appData), _configurationService.Configuration.EncodingKey);
		}

		private string GetEncodedNewRelicId(string referrerCrossProcessId)
		{
			return Strings.Base64Encode(referrerCrossProcessId, _configurationService.Configuration.EncodingKey);
		}

		private string GetEncodedTransactionData(IInternalTransaction transaction)
		{
			var txMetadata = transaction.TransactionMetadata;
			// If CrossApplicationReferrerTripId is null, then this is the first transaction to make an external request. In this case, use its Guid as the tripId.
			var tripId = txMetadata.CrossApplicationReferrerTripId ?? transaction.Guid;
			var transactionData = new CrossApplicationRequestData(transaction.Guid, false, tripId, txMetadata.LatestCrossApplicationPathHash);

			return HeaderEncoder.EncodeSerializedData(JsonConvert.SerializeObject(transactionData), _configurationService.Configuration.EncodingKey);
		}

		private Boolean IsTrustedCrossProcessAccountId(string accountId, IEnumerable<Int64> trustedAccountIds)
		{
			Int64 requestAccountId;
			if (!Int64.TryParse(accountId.Split(StringSeparators.Hash)[0], out requestAccountId))
			{
				return false;
			}

			if (!trustedAccountIds.Contains(requestAccountId))
			{
				return false;
			}

			return true;
		}
	}
}
