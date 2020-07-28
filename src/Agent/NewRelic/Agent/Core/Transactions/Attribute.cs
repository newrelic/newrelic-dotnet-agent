using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.Transactions
{
    public class Attribute : IAttribute
    {
        private const int CUSTOM_ATTRIBUTE_VALUE_LENGTH_CLAMP = 256; //bytes

        public String Key { get { return _key; } }
        private readonly String _key;

        public Object Value { get { return _value; } }
        private readonly Object _value;

        public AttributeDestinations DefaultDestinations { get { return _defaultDestinations; } }
        private readonly AttributeDestinations _defaultDestinations;

        public readonly Boolean ExcludeForHighSecurity;

        public virtual AttributeClassification Classification { get; private set; }

        private Attribute(String key, Object value, bool excludeForHighSecurity, AttributeClassification classification, AttributeDestinations defaultDestinations = AttributeDestinations.None)
        {
            _key = key;
            _value = CheckAttributeValueForAllowedType(key, value) ? value : string.Empty;
            ExcludeForHighSecurity = excludeForHighSecurity;
            Classification = classification;
            _defaultDestinations = defaultDestinations;
        }

        #region "Private builder helpers"
        private static Object TruncateUserProvidedValue(Object value)
        {
            var valueAsString = value as String;
            if (valueAsString == null)
                return value;

            return TruncateUserProvidedValue(valueAsString);
        }
        private static String TruncateUserProvidedValue(String value)
        {
            return new String(value
                .TakeWhile((c, i) =>
                    Encoding.UTF8.GetByteCount(value.Substring(0, i + 1)) <= CUSTOM_ATTRIBUTE_VALUE_LENGTH_CLAMP)
                .ToArray());
        }

        /// <summary>
        /// Dirac only accepts Strings, Singles, and Doubles.
        /// </summary>
        private Boolean CheckAttributeValueForAllowedType(String key, Object value)
        {
            if (value is Single || value is Double || value is String)
                return true;

            Log.WarnFormat("Attribute at key {0} of type {1} not allowed.  Only String and Single types accepted as attributes.", key, value.GetType());
            return false;
        }

        #endregion

        #region "Attribute Builders"
        public static Attribute BuildQueueWaitTimeAttribute(TimeSpan queueTime)
        {
            const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent;

            var value = queueTime.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
            return new Attribute("queue_wait_time_ms", value, false, AttributeClassification.AgentAttributes, destinations);
        }
        public static Attribute BuildQueueDurationAttribute(TimeSpan queueTime)
        {
            const AttributeDestinations destinations =
                AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
            var value = queueTime.TotalSeconds;
            return new Attribute("queueDuration", value, false, AttributeClassification.Intrinsics, destinations);
        }
        public static Attribute BuildOriginalUrlAttribute(string value)
        {
            const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent;
            return new Attribute("original_url", value, false, AttributeClassification.AgentAttributes, destinations);
        }
        public static Attribute BuildRequestUriAttribute(string value)
        {
            const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
            return new Attribute("request_uri", value, false, AttributeClassification.AgentAttributes, destinations);
        }
        public static Attribute BuildRequestRefererAttribute(string value)
        {
            const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent;
            return new Attribute("request.referer", value, false, AttributeClassification.AgentAttributes, destinations);
        }
        public static Attribute BuildRequestUsernameAttribute(string value)
        {
            return new Attribute("identity.username", value, true, AttributeClassification.AgentAttributes, AttributeDestinations.None);
        }
        public static Attribute BuildRequestParameterAttribute(string key, string value)
        {
            key = TruncateUserProvidedValue("request.parameters." + key);
            value = TruncateUserProvidedValue(value);
            return new Attribute(key, value, true, AttributeClassification.AgentAttributes, AttributeDestinations.None);
        }
        public static Attribute BuildServiceRequestAttribute(string key, string value)
        {
            key = TruncateUserProvidedValue("service.request." + key);
            value = TruncateUserProvidedValue(value);
            return new Attribute(key, value, true, AttributeClassification.AgentAttributes, AttributeDestinations.None);
        }
        public static Attribute BuildResponseStatusAttribute(String value)
        {
            const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
            return new Attribute("response.status", value, false, AttributeClassification.AgentAttributes, destinations);
        }
        public static Attribute BuildClientCrossProcessIdAttribute(String value)
        {
            const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace;
            return new Attribute("client_cross_process_id", value, false, AttributeClassification.Intrinsics, destinations);
        }
        public static IEnumerable<Attribute> BuildCatTripIdAttribute(String value)
        {
            return new[]
            {
                new Attribute("trip_id", value, false, AttributeClassification.Intrinsics, AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace),
                new Attribute("nr.tripId", value, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent)
            };
        }
        public static IEnumerable<Attribute> BuildBrowserTripIdAttribute(String value)
        {
            return new[]
            {
                new Attribute("nr.tripId", value, false, AttributeClassification.AgentAttributes, AttributeDestinations.JavaScriptAgent)
            };
        }
        public static IEnumerable<Attribute> BuildCatPathHash(String value)
        {
            return new[]
            {
                new Attribute("path_hash", value, false, AttributeClassification.Intrinsics, AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace),
                new Attribute("nr.pathHash", value, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent)
            };
        }
        public static IEnumerable<Attribute> BuildCatReferringPathHash(String value)
        {
            return new[]
            {
                new Attribute("nr.referringPathHash", value, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent)
            };
        }
        public static IEnumerable<Attribute> BuildCatReferringTransactionGuidAttribute(String value)
        {
            return new[]
            {
                new Attribute("referring_transaction_guid", value, false, AttributeClassification.Intrinsics, AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace),
                new Attribute("nr.referringTransactionGuid", value, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent)
            };
        }
        public static IEnumerable<Attribute> BuildCatAlternatePathHashes(String value)
        {
            return new[]
            {
                new Attribute("nr.alternatePathHashes", value, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent)
            };
        }
        public static Attribute BuildCustomErrorAttribute(string key, object value)
        {
            key = TruncateUserProvidedValue(key);
            value = TruncateUserProvidedValue(value);
            const AttributeDestinations destinations = AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace;
            return new Attribute(key, value, true, AttributeClassification.UserAttributes, destinations);
        }
        public static Attribute BuildErrorTypeAttribute(String errorType)
        {
            const AttributeDestinations destinations = AttributeDestinations.TransactionEvent;
            return new Attribute("errorType", errorType, false, AttributeClassification.Intrinsics, destinations);
        }
        public static Attribute BuildErrorMessageAttribute(String errorMessage)
        {
            return new Attribute("errorMessage", errorMessage, true, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
        }
        public static Attribute BuildTimeStampAttribute(DateTime startTime)
        {
            const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
            return new Attribute("timestamp", startTime.ToUnixTimeSeconds(), false, AttributeClassification.Intrinsics, destinations);
        }
        public static IEnumerable<Attribute> BuildTransactionNameAttribute(String transactionName)
        {
            return new[]
            {
                new Attribute("name", transactionName, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent),
                new Attribute("transactionName", transactionName, false, AttributeClassification.Intrinsics, AttributeDestinations.ErrorEvent)
            };
        }
        public static Attribute BuildGuidAttribute(String guid)
        {
            const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
            return new Attribute("nr.guid", guid, false, AttributeClassification.Intrinsics, destinations);
        }
        public static IEnumerable<Attribute> BuildSyntheticsResourceIdAttributes(String syntheticsResourceId)
        {
            return new[]
            {
                new Attribute("nr.syntheticsResourceId", syntheticsResourceId, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent),
                new Attribute("synthetics_resource_id", syntheticsResourceId, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace)
            };
        }
        public static IEnumerable<Attribute> BuildSyntheticsJobIdAttributes(String syntheticsJobId)
        {
            return new[]
            {
                new Attribute("nr.syntheticsJobId", syntheticsJobId, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent),
                new Attribute("synthetics_job_id", syntheticsJobId, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace)
            };
        }
        public static IEnumerable<Attribute> BuildSyntheticsMonitorIdAttributes(String syntheticsMonitorId)
        {
            return new[]
{
                new Attribute("nr.syntheticsMonitorId", syntheticsMonitorId, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent),
                new Attribute("synthetics_monitor_id", syntheticsMonitorId, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace)
            };
        }
        public static Attribute BuildDurationAttribute(TimeSpan transactionDuration)
        {
            var value = transactionDuration.TotalSeconds;
            return new Attribute("duration", value, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent);
        }
        public static Attribute BuildWebDurationAttribute(TimeSpan webTransactionDuration)
        {
            var value = webTransactionDuration.TotalSeconds;
            return new Attribute("webDuration", value, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
        }
        public static Attribute BuildTotalTime(TimeSpan totalTime)
        {
            var value = totalTime.TotalSeconds;
            return new Attribute("totalTime", value, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.TransactionTrace);
        }
        public static Attribute BuildCpuTime(TimeSpan cpuTime)
        {
            var value = cpuTime.TotalSeconds;
            return new Attribute("cpuTime", value, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.TransactionTrace);
        }
        public static Attribute BuildApdexPerfZoneAttribute(String apdexPerfZone)
        {
            return new Attribute("nr.apdexPerfZone", apdexPerfZone, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
        }
        public static Attribute BuildExternalDurationAttribute(Single durationInSec)
        {
            const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
            return new Attribute("externalDuration", durationInSec, false, AttributeClassification.Intrinsics, destinations);
        }
        public static Attribute BuildExternalCallCountAttribute(Single count)
        {
            const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
            return new Attribute("externalCallCount", count, false, AttributeClassification.Intrinsics, destinations);
        }
        public static Attribute BuildDatabaseDurationAttribute(Single durationInSec)
        {
            const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
            return new Attribute("databaseDuration", durationInSec, false, AttributeClassification.Intrinsics, destinations);
        }
        public static Attribute BuildDatabaseCallCountAttribute(Single count)
        {
            const AttributeDestinations destinations = AttributeDestinations.ErrorEvent | AttributeDestinations.TransactionEvent;
            return new Attribute("databaseCallCount", count, false, AttributeClassification.Intrinsics, destinations);
        }
        public static Attribute BuildErrorClassAttribute(String errorClass)
        {
            return new Attribute("error.class", errorClass, false, AttributeClassification.Intrinsics, AttributeDestinations.ErrorEvent);
        }
        public static Attribute BuildTypeAttribute(TypeAttributeValue typeAttribute)
        {
            AttributeDestinations destinations = AttributeDestinations.None;

            if (typeAttribute == TypeAttributeValue.TransactionError)
                destinations |= AttributeDestinations.ErrorEvent;
            else if (typeAttribute == TypeAttributeValue.Transaction)
                destinations |= AttributeDestinations.TransactionEvent;

            return new Attribute("type", Enum.GetName(typeof(TypeAttributeValue), typeAttribute), false, AttributeClassification.Intrinsics, destinations);
        }

        /// <summary>
        /// LOCATION: CustomAttribute
        /// TYPE: UserAttribute
        /// </summary>
        public static Attribute BuildCustomAttribute(string key, object value)
        {
            key = TruncateUserProvidedValue(key);
            value = TruncateUserProvidedValue(value);
            return new Attribute(key, value, true, AttributeClassification.UserAttributes, AttributeDestinations.All);
        }
        public static Attribute BuildErrorDotMessageAttribute(String errorMessage)
        {
            return new Attribute("error.message", errorMessage, true, AttributeClassification.Intrinsics, AttributeDestinations.ErrorEvent);
        }

        #endregion
    }

    public static class IAttributeExtensions
    {
        public static Boolean HasDestination(this IAttribute attribute, AttributeDestinations destination)
        {
            if (attribute == null)
                return false;

            return (attribute.DefaultDestinations & destination) == destination;
        }
    }
}
