// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Text;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    /// <summary>
    /// This class defines constants that are accessible to both the Agent and the Wrappers.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// This is the key-part that the agent recognizes when trying to find a DistributedTracePayload, typically passed as a KeyValuePair in the header of a request.
        /// Per the agent specs, the agent should accept the following variants of the header key name: newrelic, NEWRELIC, Newrelic
        /// </summary>
        public const string DistributedTracePayloadKeyAllLower = "newrelic";
        public const string DistributedTracePayloadKeyAllUpper = "NEWRELIC";
        public const string DistributedTracePayloadKeySingleUpper = "Newrelic";

        public const string TraceParentHeaderKey = "traceparent";

        public const string TraceStateHeaderKey = "tracestate";

    }

    public static class Statics
    {
        public static readonly string[] DefaultCaptureHeaders = { "Referer", "Accept", "Content-Length", "Host", "User-Agent" };
    }

    public enum WebTransactionType
    {
        Action,
        Custom,
        ASP,
        MVC,
        WCF,
        Razor,
        WebAPI,
        WebService,
        MonoRail,
        OpenRasta,
        StatusCode
    }

    public enum MessageBrokerDestinationType
    {
        Queue,
        Topic,
        TempQueue,
        TempTopic,
    }

    public enum MessageBrokerAction
    {
        Produce,
        Consume,
        Peek,
        Purge,
    }

    ///<summary>This enum must be a sequence of values starting with 0 and incrementing by 1. See MetricNames.GetEnumerationFunc</summary>
    public enum DatastoreVendor
    {
        //Cassandra,
        Couchbase,
        //Derby,
        //Firebird,
        IBMDB2,
        //Informix,
        Memcached,
        MongoDB,
        MySQL,
        MSSQL,
        Oracle,
        Postgres,
        Redis,
        //SQLite,
        CosmosDB,
        Elasticsearch,
        Other
    }

    public static class DatastoreVendorExtensions
    {
        // Convert our internal enum to the matching OTel "known" name for a database provider
        public static string ToKnownName(this DatastoreVendor vendor)
        {
            switch (vendor)
            {
                case DatastoreVendor.Other:
                    return "other_sql";
                case DatastoreVendor.IBMDB2:
                    return "db2";
                // The others match our enum name
                default:
                    return EnumNameCache<DatastoreVendor>.GetNameToLower(vendor);
            }
        }
    }

    public static class EnumNameCache<TEnum> // c# 7.3: where TEnum : System.Enum	
    {
        private static readonly ConcurrentDictionary<TEnum, string> Cache = new ConcurrentDictionary<TEnum, string>();
        private static readonly ConcurrentDictionary<TEnum, string> ToLowerCache = new ConcurrentDictionary<TEnum, string>();
        private static readonly ConcurrentDictionary<TEnum, string> ToUpperSnakeCaseCache = new ConcurrentDictionary<TEnum, string>();

        public static string GetName(TEnum enumValue)
        {
            return Cache.GetOrAdd(enumValue, (enumVal) => enumVal.ToString());
        }

        public static string GetNameToLower(TEnum enumValue)
        {
            return ToLowerCache.GetOrAdd(enumValue, (enumVal) => enumVal.ToString().ToLower());
        }

        public static string GetNameToUpperSnakeCase(TEnum enumValue)
        {
            return ToUpperSnakeCaseCache.GetOrAdd(enumValue, ConvertToUpperSnakeCase);
        }

        private static string ConvertToUpperSnakeCase(TEnum enumValue)
        {
            var enumValueAsString = enumValue.ToString();
            var upperSnakeCasedString = new StringBuilder();

            var previousCharIsUpper = true; //This is true so that the first char, if upper case, will not get '_' prepended
            foreach (var ch in enumValueAsString)
            {
                bool isUpper = char.IsUpper(ch);

                if (!previousCharIsUpper && isUpper)
                {
                    upperSnakeCasedString.Append('_');
                }

                upperSnakeCasedString.Append(isUpper ? ch : char.ToUpper(ch));

                previousCharIsUpper = isUpper;
            }

            return upperSnakeCasedString.ToString();
        }
    }

    //This enumeration must exactly match the equivalent enumeration defined in NewRelic.Api.Agent.TransportType.
    public enum TransportType
    {
        Unknown = 0,
        HTTP = 1,
        HTTPS = 2,
        Kafka = 3,
        JMS = 4,
        IronMQ = 5,
        AMQP = 6,
        Queue = 7,
        Other = 8
    }
}
