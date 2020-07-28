using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Utils;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing
{
    public interface ICatHeaderHandler
    {
        IEnumerable<KeyValuePair<String, String>> TryGetOutboundRequestHeaders(ITransaction transaction);
        IEnumerable<KeyValuePair<String, String>> TryGetOutboundResponseHeaders(ITransaction transaction, TransactionMetricName transactionMetricName);
        CrossApplicationResponseData TryDecodeInboundResponseHeaders(IDictionary<String, String> headers);
        String TryDecodeInboundRequestHeadersForCrossProcessId(IDictionary<String, String> headers);
        CrossApplicationRequestData TryDecodeInboundRequestHeaders(IDictionary<String, String> headers);
    }

    public class CatHeaderHandler : ICatHeaderHandler
    {
        private const String NewRelicIdHttpHeader = "X-NewRelic-ID";
        private const String TransactionDataHttpHeader = "X-NewRelic-Transaction";
        private const String AppDataHttpHeader = "X-NewRelic-App-Data";
        private readonly IConfigurationService _configurationService;

        public CatHeaderHandler(IConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        public IEnumerable<KeyValuePair<String, String>> TryGetOutboundRequestHeaders(ITransaction transaction)
        {
            try
            {
                if (!_configurationService.Configuration.CrossApplicationTracingEnabled)
                    return Enumerable.Empty<KeyValuePair<String, String>>();

                var crossProcessId = _configurationService.Configuration.CrossApplicationTracingCrossProcessId;
                if (crossProcessId == null)
                {
                    Log.Error("Failed to get cross process id for outbound request.");
                    return Enumerable.Empty<KeyValuePair<String, String>>();
                }

                var encodedNewRelicId = GetEncodedNewRelicId(crossProcessId);
                var encodedTransactionData = GetEncodedTransactionData(transaction);

                return new Dictionary<String, String>
                {
                    {NewRelicIdHttpHeader, encodedNewRelicId},
                    {TransactionDataHttpHeader, encodedTransactionData}
                };
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to get encoded CAT headers for outbound request: {ex}");
                return Enumerable.Empty<KeyValuePair<String, String>>();
            }
        }

        public IEnumerable<KeyValuePair<String, String>> TryGetOutboundResponseHeaders(ITransaction transaction, TransactionMetricName transactionMetricName)
        {
            try
            {
                if (!_configurationService.Configuration.CrossApplicationTracingEnabled)
                    return Enumerable.Empty<KeyValuePair<String, String>>();

                var refereeCrossProcessId = _configurationService.Configuration.CrossApplicationTracingCrossProcessId;
                if (refereeCrossProcessId == null)
                {
                    Log.Error("Failed to get cross process id for outbound response.");
                    return Enumerable.Empty<KeyValuePair<String, String>>();
                }

                var encodedAppData = GetEncodedAppData(transaction, transactionMetricName, refereeCrossProcessId);

                return new Dictionary<String, String>
                {
                    {AppDataHttpHeader, encodedAppData},
                };
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to get encoded CAT headers for outbound response: {ex}");
                return Enumerable.Empty<KeyValuePair<String, String>>();
            }
        }

        public CrossApplicationResponseData TryDecodeInboundResponseHeaders(IDictionary<String, String> headers)
        {
            if (!_configurationService.Configuration.CrossApplicationTracingEnabled)
                return null;

            var responseHeader = headers.GetValueOrDefault(AppDataHttpHeader);
            if (responseHeader == null)
                return null;

            return HeaderEncoder.TryDecodeAndDeserialize<CrossApplicationResponseData>(responseHeader, _configurationService.Configuration.EncodingKey);
        }

        public String TryDecodeInboundRequestHeadersForCrossProcessId(IDictionary<String, String> headers)
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

        public CrossApplicationRequestData TryDecodeInboundRequestHeaders(IDictionary<String, String> headers)
        {
            if (!_configurationService.Configuration.CrossApplicationTracingEnabled)
                return null;

            var encodedTransactionDataHttpHeader = headers.GetValueOrDefault(TransactionDataHttpHeader);
            if (encodedTransactionDataHttpHeader == null)
                return null;

            return HeaderEncoder.TryDecodeAndDeserialize<CrossApplicationRequestData>(encodedTransactionDataHttpHeader, _configurationService.Configuration.EncodingKey);
        }

        private String TryDecodeNewRelicIdHttpHeader(String encodedNewRelicIdHttpHeader)
        {
            if (encodedNewRelicIdHttpHeader == null)
                return null;

            return Strings.TryBase64Decode(encodedNewRelicIdHttpHeader, _configurationService.Configuration.EncodingKey);
        }

        private String GetEncodedAppData(ITransaction transaction, TransactionMetricName transactionMetricName, String crossProcessId)
        {
            var txMetadata = transaction.TransactionMetadata;
            var queueTime = txMetadata.QueueTime?.TotalSeconds ?? 0;
            var referrerContentLength = txMetadata.GetCrossApplicationReferrerContentLength();
            var appData = new CrossApplicationResponseData(crossProcessId, transactionMetricName.PrefixedName, (float)queueTime, (float)transaction.GetDurationUntilNow().TotalSeconds, referrerContentLength, transaction.Guid);

            return HeaderEncoder.SerializeAndEncode(appData, _configurationService.Configuration.EncodingKey);
        }

        private String GetEncodedNewRelicId(String referrerCrossProcessId)
        {
            return Strings.Base64Encode(referrerCrossProcessId, _configurationService.Configuration.EncodingKey);
        }

        private String GetEncodedTransactionData(ITransaction transaction)
        {
            var txMetadata = transaction.TransactionMetadata;
            // If CrossApplicationReferrerTripId is null, then this is the first transaction to make an external request. In this case, use its Guid as the tripId.
            var tripId = txMetadata.CrossApplicationReferrerTripId ?? transaction.Guid;
            var transactionData = new CrossApplicationRequestData(transaction.Guid, false, tripId, txMetadata.LatestCrossApplicationPathHash);
            return HeaderEncoder.SerializeAndEncode(transactionData, _configurationService.Configuration.EncodingKey);
        }

        private Boolean IsTrustedCrossProcessAccountId(String accountId, IEnumerable<Int64> trustedAccountIds)
        {
            Int64 requestAccountId;
            if (!Int64.TryParse(accountId.Split('#').FirstOrDefault(), out requestAccountId))
                return false;
            if (!trustedAccountIds.Contains(requestAccountId))
                return false;
            return true;
        }
    }
}
