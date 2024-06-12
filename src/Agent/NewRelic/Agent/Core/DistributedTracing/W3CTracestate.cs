// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.DistributedTracing
{
    public class W3CTracestate
    {
        // VendorstateEntries:
        //    {
        //        { "rojo", "00f067aa0ba902b7" },
        //        { "congo", "t61rcWkgMzE" },
        //        { "abc", "ujv" },
        //        { "xyz", "mmm" },
        //    }
        // ingested order must be maintained for outgoing header

        private const string NRVendorString = "@nr";
        private const int NumberOfFieldsInSupportedVersion = 9;
        private const int SupportedVersion = 0;
        private const int VersionIndex = 0;
        private const int ParentTypeIndex = 1;
        private const int AccountIdIndex = 2;
        private const int AppIdIndex = 3;
        private const int SpanIdIndex = 4;
        private const int TransactionIdIndex = 5;
        private const int SampledIndex = 6;
        private const int PriorityIndex = 7;
        private const int MaxDecimalPlacesInPriority = 6;
        private const int TimestampIndex = 8;

        public List<string> VendorstateEntries { get; set; }    // nonNR, nonTrusted: 55@dd=string, 45@nr=string

        // fields pulled from the trusted tracestate NR entry
        public string AccountKey { get; set; }  // "33" from "33@nr" aka trusted_account_key, tenant_id
        public int Version { get; set; }
        public DistributedTracingParentType ParentType { get; set; }
        public string AccountId { get; set; }
        public string AppId { get; set; }
        public string SpanId { get; set; }
        public string TransactionId { get; set; }
        public int? Sampled { get; set; }
        public float? Priority { get; set; }
        public long Timestamp { get; set; }

        public IngestErrorType Error { get; }

        public W3CTracestate(List<string> vendorstates, string accountKey, int version, int parentType, string accountId, string appId, string spanId, string transactionId, int? sampled, float? priority, long timestamp) :
            this(vendorstates, accountKey, version, parentType, accountId, appId, spanId, transactionId, sampled, priority, timestamp, IngestErrorType.None)
        {
        }

        private W3CTracestate(List<string> vendorstates, string accountKey, int version, int parentType, string accountId, string appId, string spanId, string transactionId, int? sampled, float? priority, long timestamp, IngestErrorType error)
        {
            VendorstateEntries = vendorstates;
            AccountKey = accountKey;
            Version = version;
            ParentType = (DistributedTracingParentType)parentType;
            AccountId = accountId;
            AppId = appId;
            SpanId = spanId;
            TransactionId = transactionId;
            Sampled = sampled;
            Priority = priority;
            Timestamp = timestamp;

            Error = error;
        }

        public override string ToString() => $"{AccountId}@nr={Version}-{(int)ParentType}-{AccountId}-{AppId}-{SpanId}-{TransactionId}-{Sampled}-" + Priority?.ToString(System.Globalization.CultureInfo.InvariantCulture) + $"-{Timestamp}";

        public static W3CTracestate GetW3CTracestateFromHeaders(IEnumerable<string> tracestateCollection, string trustedAccountKey)
        {
            var tracestateEntries = TryExtractTracestateHeaders(tracestateCollection);

            if (tracestateEntries.Count > 0)
            {
                var newRelicTraceStateEntry = tracestateEntries.Where(entry => entry.Key.Equals($"{trustedAccountKey}{NRVendorString}")).FirstOrDefault();

                var vendorstates = tracestateEntries.Where(entry => !entry.Key.Contains($"{trustedAccountKey}{NRVendorString}")).Select(entry => $"{entry.Key}={entry.Value}").ToList();

                if (string.IsNullOrEmpty(newRelicTraceStateEntry.Value))
                {
                    return new W3CTracestate(vendorstates, null, default, (int)DistributedTracingParentType.Unknown, null, null, null, null, default, default, default, IngestErrorType.TraceStateNoNrEntry);
                }

                var traceStateWithInvalidNrEntry = new W3CTracestate(vendorstates, null, default, (int)DistributedTracingParentType.Unknown, null, null, null, null, default, default, default, IngestErrorType.TraceStateInvalidNrEntry);

                if (!TracestateUtils.ValidateValue(newRelicTraceStateEntry.Value))
                {
                    return traceStateWithInvalidNrEntry;
                }

                var splits = newRelicTraceStateEntry.Value.Split('-');
                var fieldCount = splits.Count();

                int version;

                if (!int.TryParse(splits[VersionIndex], out version) ||
                    (version == SupportedVersion && fieldCount != NumberOfFieldsInSupportedVersion) ||
                    (version > SupportedVersion && fieldCount <= NumberOfFieldsInSupportedVersion))
                {
                    return traceStateWithInvalidNrEntry;
                }

                var accountId = string.IsNullOrWhiteSpace(splits[AccountIdIndex]) ? null : splits[AccountIdIndex];
                var appId = string.IsNullOrWhiteSpace(splits[AppIdIndex]) ? null : splits[AppIdIndex];
                var spanId = string.IsNullOrWhiteSpace(splits[SpanIdIndex]) ? null : splits[SpanIdIndex];
                var transactionId = string.IsNullOrWhiteSpace(splits[TransactionIdIndex]) ? null : splits[TransactionIdIndex];

                //required fields
                if (!int.TryParse(splits[ParentTypeIndex], out int parentType) ||
                    parentType < (int)DistributedTracingParentType.App ||
                    parentType > (int)DistributedTracingParentType.Mobile ||
                    string.IsNullOrEmpty(accountId) ||
                    string.IsNullOrEmpty(appId))
                {
                    return traceStateWithInvalidNrEntry;
                }

                int? sampled = null;

                if (!string.IsNullOrEmpty(splits[SampledIndex]) &&
                    int.TryParse(splits[SampledIndex], out int parsedSampled) &&
                    (parsedSampled == (int)SampledEnum.IsTrue || parsedSampled == (int)SampledEnum.IsFalse))
                {
                    sampled = parsedSampled;
                }

                float? priority = null;

                if (!string.IsNullOrEmpty(splits[PriorityIndex]) && TryParseAndValidatePriority(splits[PriorityIndex], out float parsedPriority))
                {
                    priority = parsedPriority;
                }

                //required field
                if (!long.TryParse(splits[TimestampIndex], out long timestamp))
                {
                    return traceStateWithInvalidNrEntry;
                }

                return new W3CTracestate(vendorstates, trustedAccountKey, version, parentType,
                    accountId, appId, spanId, transactionId, sampled, priority, timestamp);
            }

            return new W3CTracestate(null, null, default, default, null, null, null, null, null, null, default, IngestErrorType.TraceStateNoNrEntry);
        }

        private static List<KeyValuePair<string, string>> TryExtractTracestateHeaders(IEnumerable<string> tracestateCollection)
        {
            List<KeyValuePair<string, string>> tracestateEntries = new List<KeyValuePair<string, string>>();

            if (tracestateCollection != null)
            {
                // Iterate in reverse order.
                foreach (var tracestateValue in tracestateCollection.Reverse())
                {
                    if (!TracestateUtils.ParseTracestate(tracestateValue, tracestateEntries))
                    {
                        break;
                    }
                }
            }

            return tracestateEntries;
        }

        private static bool TryParseAndValidatePriority(string priorityString, out float priority)
        {
            priority = default;
            var lastIndex = priorityString.Length - 1;

            //Checking if priority value is rounded to 6 decimal places
            if (priorityString.IndexOf('.') > -1)
            {
                priorityString = priorityString.TrimEnd(new char[] { '0' });

                if (lastIndex - priorityString.IndexOf('.') > MaxDecimalPlacesInPriority)
                {
                    return false;
                }
            }

            if (float.TryParse(priorityString, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out priority))
            {
                return true;
            }

            return false;
        }
    }

    enum SampledEnum
    {
        IsFalse,
        IsTrue
    }
}
