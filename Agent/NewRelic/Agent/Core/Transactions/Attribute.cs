using NewRelic.Core;
using NewRelic.Core.Logging;
using NewRelic.SystemExtensions;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace NewRelic.Agent.Core.Transactions
{
	public class Attribute : IAttribute
	{
		private const int CUSTOM_ATTRIBUTE_VALUE_LENGTH_CLAMP = 255; //bytes

		public string Key { get { return _key; } }

		private readonly string _key;

		public object Value { get { return _value; } }

		private readonly object _value;

		public AttributeDestinations DefaultDestinations { get { return _defaultDestinations; } }
		private readonly AttributeDestinations _defaultDestinations;

		public virtual AttributeClassification Classification { get; private set; }

		private Attribute(string key, object value, AttributeClassification classification, AttributeDestinations defaultDestinations)
		{
			_key = key;
			_value = CheckAttributeValueForAllowedType(key, value) ? value : string.Empty;
			Classification = classification;
			_defaultDestinations = defaultDestinations;
		}

		private const AttributeDestinations AllTracesAndEventsDestinations = AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorTrace | AttributeDestinations.SqlTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;

		private static object TruncateUserProvidedValue(object value, uint maxBytes)
		{
			var valueAsString = value as string;
			if (valueAsString == null)
				return value;

			return valueAsString.TruncateUnicodeStringByBytes(maxBytes);
		}

		private bool CheckAttributeValueForAllowedType(string key, object value)
		{
			var type = value?.GetType();
			var typeCode = Type.GetTypeCode(type);
			if (typeCode == TypeCode.String || typeCode >= TypeCode.Boolean && typeCode <= TypeCode.Decimal && typeCode != TypeCode.Char)
				return true;

			Log.WarnFormat("Attribute at key {0} of type {1} not allowed.  Only string, bool, and floating point and integral types are acceptable as attributes.", key, type?.ToString() ?? "null");
			return false;
		}

		#region "Attribute Builders"

		public static Attribute BuildQueueWaitTimeAttribute(TimeSpan queueTime)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent;

			var value = queueTime.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
			return new Attribute("queue_wait_time_ms", value, AttributeClassification.AgentAttributes, destinations);
		}

		public static Attribute BuildQueueDurationAttribute(TimeSpan queueTime)
		{
			const AttributeDestinations destinations =
				AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			var value = queueTime.TotalSeconds;
			return new Attribute("queueDuration", value, AttributeClassification.Intrinsics, destinations);
		}

		public static Attribute BuildOriginalUrlAttribute(string value)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent;
			return new Attribute("original_url", value, AttributeClassification.AgentAttributes, destinations);
		}

		public static Attribute BuildRequestUriAttribute(string value)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.SqlTrace;
			return new Attribute("request.uri", value, AttributeClassification.AgentAttributes, destinations);
		}

		public static Attribute BuildRequestRefererAttribute(string value)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent;
			return new Attribute("request.referer", value, AttributeClassification.AgentAttributes, destinations);
		}

		/// <summary>
		/// Warning: We do not prevent the capturing of request parameters based on our configuration settings.
		/// Instead, we rely on the configuration settings being set correctly to control the inclusion of these attributes.
		/// See DefaultConfiguration.CaptureTransactionTraceAttributesIncludes for an example of how the inclusion
		/// of these attributes are controlled.
		/// <para>
		/// Constructs a request.parameter.{key} attribute with the provided {value}.
		/// </para>
		/// </summary>
		/// <param name="key">Name of the request parameter</param>
		/// <param name="value">Value of the attribute</param>
		/// <returns>The constructed attribute.</returns>
		public static Attribute BuildRequestParameterAttribute(string key, string value)
		{
			key = ("request.parameters." + key).TruncateUnicodeStringByBytes(CUSTOM_ATTRIBUTE_VALUE_LENGTH_CLAMP);
			value = value.TruncateUnicodeStringByBytes(CUSTOM_ATTRIBUTE_VALUE_LENGTH_CLAMP);
			return new Attribute(key, value, AttributeClassification.AgentAttributes, AttributeDestinations.None);
		}

		public static Attribute BuildResponseStatusAttribute(string value)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			return new Attribute("response.status", value, AttributeClassification.AgentAttributes, destinations);
		}

		public static Attribute BuildClientCrossProcessIdAttribute(string value)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace;
			return new Attribute("client_cross_process_id", value, AttributeClassification.Intrinsics, destinations);
		}

		public static Attribute BuildTripUnderscoreIdAttribute(string value)
		{
			return new Attribute("trip_id", value, AttributeClassification.Intrinsics, AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace);
		}

		public static Attribute BuildCatNrTripIdAttribute(string value)
		{
			return new Attribute("nr.tripId", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
		}

		public static IEnumerable<Attribute> BuildBrowserTripIdAttribute(string value)
		{
			return new[]
			{
				new Attribute("nr.tripId", value, AttributeClassification.AgentAttributes, AttributeDestinations.JavaScriptAgent)
			};
		}

		public static IEnumerable<Attribute> BuildCatPathHash(string value)
		{
			return new[]
			{
				new Attribute("path_hash", value, AttributeClassification.Intrinsics, AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace),
				new Attribute("nr.pathHash", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent)
			};
		}

		public static IEnumerable<Attribute> BuildCatReferringPathHash(string value)
		{
			return new[]
			{
				new Attribute("nr.referringPathHash", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent)
			};
		}

		public static IEnumerable<Attribute> BuildCatReferringTransactionGuidAttribute(string value)
		{
			return new[]
			{
				new Attribute("referring_transaction_guid", value, AttributeClassification.Intrinsics, AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace),
				new Attribute("nr.referringTransactionGuid", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent)
			};
		}

		public static IEnumerable<Attribute> BuildCatAlternatePathHashes(string value)
		{
			return new[]
			{
				new Attribute("nr.alternatePathHashes", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent)
			};
		}

		public static Attribute BuildCustomErrorAttribute(string key, object value)
		{
			key = key.TruncateUnicodeStringByBytes(CUSTOM_ATTRIBUTE_VALUE_LENGTH_CLAMP);
			value = TruncateUserProvidedValue(value, CUSTOM_ATTRIBUTE_VALUE_LENGTH_CLAMP);
			const AttributeDestinations destinations = AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace;
			return new Attribute(key, value, AttributeClassification.UserAttributes, destinations);
		}

		public static Attribute BuildErrorTypeAttribute(string errorType)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent;
			return new Attribute("errorType", errorType, AttributeClassification.Intrinsics, destinations);
		}

		public static Attribute BuildErrorMessageAttribute(string errorMessage)
		{
			return new Attribute("errorMessage", errorMessage, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
		}

		public static Attribute BuildErrorAttribute(bool isError)
		{
			return new Attribute("error", isError, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
		}

		public static Attribute BuildTransactionTimeStampAttribute(DateTime startTime)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent;
			return new Attribute("timestamp", startTime.ToUnixTimeMilliseconds(), AttributeClassification.Intrinsics, destinations);
		}

		public static Attribute BuildErrorTimeStampAttribute(DateTime errorTime)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorEvent;
			return new Attribute("timestamp", errorTime.ToUnixTimeMilliseconds(), AttributeClassification.Intrinsics, destinations);
		}

		public static IEnumerable<Attribute> BuildTransactionNameAttribute(string transactionName)
		{
			return new[]
			{
				new Attribute("name", transactionName, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent),
				new Attribute("transactionName", transactionName, AttributeClassification.Intrinsics, AttributeDestinations.ErrorEvent)
			};
		}

		public static Attribute BuildTransactionNameAttributeForCustomError(string name)
		{
			return new Attribute("transactionName", name, AttributeClassification.Intrinsics, AttributeDestinations.ErrorEvent);
		}

		public static Attribute BuildNrGuidAttribute(string guid)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			return new Attribute("nr.guid", guid, AttributeClassification.Intrinsics, destinations);
		}

		public static Attribute BuildGuidAttribute(string guid)
		{
			return new Attribute("guid", guid, AttributeClassification.Intrinsics, AllTracesAndEventsDestinations);
		}

		public static IEnumerable<Attribute> BuildSyntheticsResourceIdAttributes(string syntheticsResourceId)
		{
			return new[]
			{
				new Attribute("nr.syntheticsResourceId", syntheticsResourceId, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent),
				new Attribute("synthetics_resource_id", syntheticsResourceId, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace)
			};
		}

		public static IEnumerable<Attribute> BuildSyntheticsJobIdAttributes(string syntheticsJobId)
		{
			return new[]
			{
				new Attribute("nr.syntheticsJobId", syntheticsJobId, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent),
				new Attribute("synthetics_job_id", syntheticsJobId, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace)
			};
		}

		public static IEnumerable<Attribute> BuildSyntheticsMonitorIdAttributes(string syntheticsMonitorId)
		{
			return new[]
			{
				new Attribute("nr.syntheticsMonitorId", syntheticsMonitorId, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent),
				new Attribute("synthetics_monitor_id", syntheticsMonitorId, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace)
			};
		}

		public static Attribute BuildDurationAttribute(TimeSpan transactionDuration)
		{
			var value = transactionDuration.TotalSeconds;
			return new Attribute("duration", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent);
		}

		public static Attribute BuildWebDurationAttribute(TimeSpan webTransactionDuration)
		{
			var value = webTransactionDuration.TotalSeconds;
			return new Attribute("webDuration", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
		}

		public static Attribute BuildTotalTime(TimeSpan totalTime)
		{
			var value = totalTime.TotalSeconds;
			return new Attribute("totalTime", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.TransactionTrace);
		}

		public static Attribute BuildCpuTime(TimeSpan cpuTime)
		{
			var value = cpuTime.TotalSeconds;
			return new Attribute("cpuTime", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.TransactionTrace);
		}

		public static Attribute BuildApdexPerfZoneAttribute(string apdexPerfZone)
		{
			return new Attribute("nr.apdexPerfZone", apdexPerfZone, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
		}

		public static Attribute BuildExternalDurationAttribute(float durationInSec)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			return new Attribute("externalDuration", durationInSec, AttributeClassification.Intrinsics, destinations);
		}

		public static Attribute BuildExternalCallCountAttribute(float count)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			return new Attribute("externalCallCount", count, AttributeClassification.Intrinsics, destinations);
		}

		public static Attribute BuildDatabaseDurationAttribute(float durationInSec)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			return new Attribute("databaseDuration", durationInSec, AttributeClassification.Intrinsics, destinations);
		}

		public static Attribute BuildDatabaseCallCountAttribute(float count)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorEvent | AttributeDestinations.TransactionEvent;
			return new Attribute("databaseCallCount", count, AttributeClassification.Intrinsics, destinations);
		}

		public static Attribute BuildErrorClassAttribute(string errorClass)
		{
			return new Attribute("error.class", errorClass, AttributeClassification.Intrinsics, AttributeDestinations.ErrorEvent);
		}

		public static Attribute BuildTypeAttribute(TypeAttributeValue typeAttribute)
		{
			AttributeDestinations destinations = AttributeDestinations.None;

			if (typeAttribute == TypeAttributeValue.TransactionError)
				destinations |= AttributeDestinations.ErrorEvent;
			else if (typeAttribute == TypeAttributeValue.Transaction)
				destinations |= AttributeDestinations.TransactionEvent;

			return new Attribute("type", Enum.GetName(typeof(TypeAttributeValue), typeAttribute), AttributeClassification.Intrinsics, destinations);
		}

		/// <summary>
		/// LOCATION: CustomAttribute
		/// TYPE: UserAttribute
		/// </summary>
		public static Attribute BuildCustomAttribute(string key, object value)
		{
			key = key.TruncateUnicodeStringByBytes(CUSTOM_ATTRIBUTE_VALUE_LENGTH_CLAMP);
			value = TruncateUserProvidedValue(value, CUSTOM_ATTRIBUTE_VALUE_LENGTH_CLAMP);
			return new Attribute(key, value, AttributeClassification.UserAttributes, AttributeDestinations.All);
		}

		public static Attribute BuildErrorDotMessageAttribute(string errorMessage)
		{
			return new Attribute("error.message", errorMessage, AttributeClassification.Intrinsics, AttributeDestinations.ErrorEvent);
		}

		public static Attribute BuildParentTypeAttribute(string value)
		{
			return new Attribute("parent.type", value, AttributeClassification.Intrinsics, AllTracesAndEventsDestinations);
		}

		public static Attribute BuildParentAccountAttribute(string value)
		{
			return new Attribute("parent.account", value, AttributeClassification.Intrinsics, AllTracesAndEventsDestinations);
		}

		public static Attribute BuildParentAppAttribute(string value)
		{
			return new Attribute("parent.app", value, AttributeClassification.Intrinsics, AllTracesAndEventsDestinations);
		}

		public static Attribute BuildParentTransportTypeAttribute(string value)
		{
			return new Attribute("parent.transportType", value, AttributeClassification.Intrinsics, AllTracesAndEventsDestinations);
		}

		public static Attribute BuildParentTransportDurationAttribute(TimeSpan value)
		{
			var durationInSeconds = (float)value.TotalSeconds;
			return new Attribute("parent.transportDuration", durationInSeconds, AttributeClassification.Intrinsics, AllTracesAndEventsDestinations);
		}

		public static Attribute BuildParentSpanIdAttribute(string value)
		{
			return new Attribute("parentSpanId", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
		}

		public static Attribute BuildParentIdAttribute(string value)
		{
			return new Attribute("parentId", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
		}

		public static Attribute BuildDistributedTraceIdAttributes(string value)
		{
			return new Attribute("traceId", value, AttributeClassification.Intrinsics, AllTracesAndEventsDestinations);
		}

		public static Attribute BuildPriorityAttribute(float value)
		{
			return new Attribute("priority", value, AttributeClassification.Intrinsics, AllTracesAndEventsDestinations);
		}

		public static Attribute BuildSampledAttribute(bool value)
		{
			return new Attribute("sampled", value, AttributeClassification.Intrinsics, AllTracesAndEventsDestinations);
		}

		public static Attribute BuildHostDisplayNameAttribute(string value)
		{
			return new Attribute("host.displayName", value, AttributeClassification.AgentAttributes, AttributeDestinations.TransactionTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.ErrorEvent);
		}

		#endregion "Attribute Builders"
	}

	public static class IAttributeExtensions
	{
		public static bool HasDestination(this IAttribute attribute, AttributeDestinations destination)
		{
			if (attribute == null)
				return false;

			return (attribute.DefaultDestinations & destination) == destination;
		}
	}
}
