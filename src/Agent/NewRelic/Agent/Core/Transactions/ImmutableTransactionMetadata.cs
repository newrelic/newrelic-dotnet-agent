using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;

namespace NewRelic.Agent.Core.Transactions
{
    public class ImmutableTransactionMetadata : ITransactionAttributeMetadata
    {
        public IEnumerable<KeyValuePair<string, string>> RequestParameters { get; }
        public IEnumerable<KeyValuePair<string, string>> ServiceParameters { get; }
        public IEnumerable<KeyValuePair<string, object>> UserAttributes { get; }
        public IEnumerable<KeyValuePair<string, object>> UserErrorAttributes { get; }

        public string Uri { get; }
        public string OriginalUri { get; }
        public string ReferrerUri { get; }
        public TimeSpan? QueueTime { get; }
        public int? HttpResponseStatusCode { get; }
        public IEnumerable<ErrorData> TransactionExceptionDatas { get; }
        public IEnumerable<ErrorData> CustomErrorDatas { get; }
        public IEnumerable<string> CrossApplicationAlternatePathHashes { get; }
        public string CrossApplicationReferrerTransactionGuid { get; }
        public string CrossApplicationReferrerPathHash { get; }
        public string CrossApplicationPathHash { get; }
        public string Path { get; }

        public string CrossApplicationReferrerProcessId { get; }
        public string CrossApplicationReferrerTripId { get; }
        public int? HttpResponseSubStatusCode { get; }

        public string SyntheticsResourceId { get; }
        public string SyntheticsJobId { get; }
        public string SyntheticsMonitorId { get; }
        public bool IsSynthetics { get; }
        public bool HasCatResponseHeaders { get; }

        public ImmutableTransactionMetadata(string uri, string originalUri, string path, string referrerUri,
            TimeSpan? queueTime, IEnumerable<KeyValuePair<string, string>> requestParameters,
            IEnumerable<KeyValuePair<string, string>> serviceParameters,
            IEnumerable<KeyValuePair<string, object>> userAttributes,
            IEnumerable<KeyValuePair<string, object>> userErrorAttributes, int? httpResponseStatusCode,
            int? httpResponseSubStatusCode, IEnumerable<ErrorData> transactionExceptionDatas,
            IEnumerable<ErrorData> customErrorDatas, string crossApplicationReferrerPathHash, string crossApplicationPathHash,
            IEnumerable<string> crossApplicationPathHashes, string crossApplicationReferrerTransactionGuid,
            string crossApplicationReferrerProcessId, string crossApplicationReferrerTripId, string syntheticsResourceId,
            string syntheticsJobId, string syntheticsMonitorId, bool isSynthetics, bool hasCatResponseHeaders)
        {
            Uri = uri;
            OriginalUri = originalUri;
            Path = path;
            ReferrerUri = referrerUri;
            QueueTime = queueTime;
            RequestParameters = requestParameters.ToList();
            ServiceParameters = serviceParameters.ToList();
            UserAttributes = userAttributes.ToList();
            UserErrorAttributes = userErrorAttributes.ToList();
            HttpResponseStatusCode = httpResponseStatusCode;
            HttpResponseSubStatusCode = httpResponseSubStatusCode;
            TransactionExceptionDatas = transactionExceptionDatas;
            CustomErrorDatas = customErrorDatas;
            CrossApplicationReferrerPathHash = crossApplicationReferrerPathHash;
            CrossApplicationPathHash = crossApplicationPathHash;
            CrossApplicationAlternatePathHashes = crossApplicationPathHashes.ToList();
            CrossApplicationReferrerTransactionGuid = crossApplicationReferrerTransactionGuid;
            CrossApplicationReferrerProcessId = crossApplicationReferrerProcessId;
            CrossApplicationReferrerTripId = crossApplicationReferrerTripId;
            SyntheticsResourceId = syntheticsResourceId;
            SyntheticsJobId = syntheticsJobId;
            SyntheticsMonitorId = syntheticsMonitorId;
            IsSynthetics = isSynthetics;
            HasCatResponseHeaders = hasCatResponseHeaders;
        }
    }
}
