using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.Transactions
{
	public class Attribute : IAttribute
	{
		private const int CUSTOM_ATTRIBUTE_VALUE_LENGTH_CLAMP = 256; //bytes

		public String Key { get { return _key; } }
		[NotNull]
		private readonly String _key;

		public Object Value { get { return _value; } }
		[NotNull]
		private readonly Object _value;

		public AttributeDestinations DefaultDestinations { get { return _defaultDestinations; } }
		private readonly AttributeDestinations _defaultDestinations;
		
		public readonly Boolean ExcludeForHighSecurity;

		public virtual AttributeClassification Classification { get; private set; }

		private Attribute([NotNull] String key, [NotNull] Object value, bool excludeForHighSecurity, AttributeClassification classification, AttributeDestinations defaultDestinations = AttributeDestinations.None)
		{
			_key = key;
			_value = CheckAttributeValueForAllowedType(key, value) ? value : string.Empty;
			ExcludeForHighSecurity = excludeForHighSecurity;
			Classification = classification;
			_defaultDestinations = defaultDestinations;
		}

		#region "Private builder helpers"
		[NotNull]
		private static Object TruncateUserProvidedValue([NotNull] Object value)
		{
			var valueAsString = value as String;
			if (valueAsString == null)
				return value;

			return TruncateUserProvidedValue(valueAsString);
		}

		[NotNull]
		private static String TruncateUserProvidedValue([NotNull] String value)
		{
			return new String(value
				.TakeWhile((c, i) =>
					Encoding.UTF8.GetByteCount(value.Substring(0, i + 1)) <= CUSTOM_ATTRIBUTE_VALUE_LENGTH_CLAMP)
				.ToArray());
		}

		/// <summary>
		/// Dirac only accepts Strings, Singles, and Doubles.
		/// </summary>
		private Boolean CheckAttributeValueForAllowedType([NotNull] String key, [NotNull] Object value)
		{
			if (value is Single || value is Double || value is String)
				return true;

			Log.WarnFormat("Attribute at key {0} of type {1} not allowed.  Only String and Single types accepted as attributes.", key, value.GetType());
			return false;
		}

		#endregion

		#region "Attribute Builders"

		[NotNull]
		public static Attribute BuildQueueWaitTimeAttribute(TimeSpan queueTime)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent;

			var value = queueTime.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
			return new Attribute("queue_wait_time_ms", value, false, AttributeClassification.AgentAttributes, destinations);
		}

		[NotNull]
		public static Attribute BuildQueueDurationAttribute(TimeSpan queueTime)
		{
			const AttributeDestinations destinations =
				AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			var value = queueTime.TotalSeconds;
			return new Attribute("queueDuration", value, false, AttributeClassification.Intrinsics, destinations);
		}

		[NotNull]
		public static Attribute BuildOriginalUrlAttribute([NotNull] string value)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent;
			return new Attribute("original_url", value, false, AttributeClassification.AgentAttributes, destinations);
		}

		[NotNull]
		public static Attribute BuildRequestUriAttribute([NotNull] string value)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			return new Attribute("request_uri", value, false, AttributeClassification.AgentAttributes, destinations);
		}

		[NotNull]
		public static Attribute BuildRequestRefererAttribute([NotNull] string value)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent;
			return new Attribute("request.referer", value, false, AttributeClassification.AgentAttributes, destinations);
		}

		[NotNull]
		public static Attribute BuildRequestUsernameAttribute([NotNull] string value)
		{
			return new Attribute("identity.username", value, true, AttributeClassification.AgentAttributes, AttributeDestinations.None);
		}

		[NotNull]
		public static Attribute BuildRequestParameterAttribute([NotNull] string key, [NotNull] string value)
		{
			key = TruncateUserProvidedValue("request.parameters." + key);
			value = TruncateUserProvidedValue(value);
			return new Attribute(key, value, true, AttributeClassification.AgentAttributes, AttributeDestinations.None);
		}

		[NotNull]
		public static Attribute BuildResponseStatusAttribute([NotNull] String value)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			return new Attribute("response.status", value, false, AttributeClassification.AgentAttributes, destinations);
		}

		[NotNull]
		public static Attribute BuildClientCrossProcessIdAttribute([NotNull] String value)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace;
			return new Attribute("client_cross_process_id", value, false, AttributeClassification.Intrinsics, destinations);
		}

		[NotNull]
		public static IEnumerable<Attribute> BuildCatTripIdAttribute([NotNull] String value)
		{
			return new[]
			{
				new Attribute("trip_id", value, false, AttributeClassification.Intrinsics, AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace),
				new Attribute("nr.tripId", value, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent)
			};
		}

		[NotNull]
		public static IEnumerable<Attribute> BuildBrowserTripIdAttribute([NotNull] String value)
		{
			return new[]
			{
				new Attribute("nr.tripId", value, false, AttributeClassification.AgentAttributes, AttributeDestinations.JavaScriptAgent)
			};
		}

		[NotNull]
		public static IEnumerable<Attribute> BuildCatPathHash([NotNull] String value)
		{
			return new[]
			{
				new Attribute("path_hash", value, false, AttributeClassification.Intrinsics, AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace),
				new Attribute("nr.pathHash", value, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent)
			};
		}

		[NotNull]
		public static IEnumerable<Attribute> BuildCatReferringPathHash([NotNull] String value)
		{
			return new[]
			{
				new Attribute("nr.referringPathHash", value, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent)
			};
		}


		[NotNull]
		public static IEnumerable<Attribute> BuildCatReferringTransactionGuidAttribute([NotNull] String value)
		{
			return new[]
			{
				new Attribute("referring_transaction_guid", value, false, AttributeClassification.Intrinsics, AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace),
				new Attribute("nr.referringTransactionGuid", value, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent)
			};
		}

		[NotNull]
		public static IEnumerable<Attribute> BuildCatAlternatePathHashes([NotNull] String value)
		{
			return new[]
			{
				new Attribute("nr.alternatePathHashes", value, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent)
			};
		}

		[NotNull]
		public static Attribute BuildCustomErrorAttribute([NotNull] string key, [NotNull] object value)
		{
			key = TruncateUserProvidedValue(key);
			value = TruncateUserProvidedValue(value);
			const AttributeDestinations destinations = AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace;
			return new Attribute(key, value, true, AttributeClassification.UserAttributes, destinations);
		}

		[NotNull]
		public static Attribute BuildErrorTypeAttribute([NotNull] String errorType)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent;
			return new Attribute("errorType", errorType, false, AttributeClassification.Intrinsics, destinations);
		}

		[NotNull]
		public static Attribute BuildErrorMessageAttribute([NotNull] String errorMessage)
		{
			return new Attribute("errorMessage", errorMessage, true, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
		}

		[NotNull]
		public static Attribute BuildTimeStampAttribute(DateTime startTime)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			return new Attribute("timestamp", startTime.ToUnixTime(), false, AttributeClassification.Intrinsics, destinations);
		}

		[NotNull]
		public static IEnumerable<Attribute> BuildTransactionNameAttribute([NotNull] String transactionName)
		{
			return new[]
			{
				new Attribute("name", transactionName, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent),
				new Attribute("transactionName", transactionName, false, AttributeClassification.Intrinsics, AttributeDestinations.ErrorEvent)
			};
		}

		[NotNull]
		public static Attribute BuildGuidAttribute([NotNull] String guid)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			return new Attribute("nr.guid", guid, false, AttributeClassification.Intrinsics, destinations);
		}

		[NotNull]
		public static IEnumerable<Attribute> BuildSyntheticsResourceIdAttributes([NotNull] String syntheticsResourceId)
		{
			return new[]
			{
				new Attribute("nr.syntheticsResourceId", syntheticsResourceId, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent),
				new Attribute("synthetics_resource_id", syntheticsResourceId, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace)
			};
		}

		[NotNull]
		public static IEnumerable<Attribute> BuildSyntheticsJobIdAttributes([NotNull] String syntheticsJobId)
		{
			return new[]
			{
				new Attribute("nr.syntheticsJobId", syntheticsJobId, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent),
				new Attribute("synthetics_job_id", syntheticsJobId, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace)
			};
		}

		[NotNull]
		public static IEnumerable<Attribute> BuildSyntheticsMonitorIdAttributes([NotNull] String syntheticsMonitorId)
		{
			return new[]
{
				new Attribute("nr.syntheticsMonitorId", syntheticsMonitorId, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent),
				new Attribute("synthetics_monitor_id", syntheticsMonitorId, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace)
			};
		}

		[NotNull]
		public static Attribute BuildDurationAttribute(TimeSpan transactionDuration)
		{
			var value = transactionDuration.TotalSeconds;
			return new Attribute("duration", value, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent);
		}

		[NotNull]
		public static Attribute BuildWebDurationAttribute(TimeSpan webTransactionDuration)
		{
			var value = webTransactionDuration.TotalSeconds;
			return new Attribute("webDuration", value, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
		}


		[NotNull]
		public static Attribute BuildTotalTime(TimeSpan totalTime)
		{
			var value = totalTime.TotalSeconds;
			return new Attribute("totalTime", value, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.TransactionTrace);
		}

		[NotNull]
		public static Attribute BuildCpuTime(TimeSpan cpuTime)
		{
			var value = cpuTime.TotalSeconds;
			return new Attribute("cpuTime", value, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.TransactionTrace);
		}

		[NotNull]
		public static Attribute BuildApdexPerfZoneAttribute([NotNull] String apdexPerfZone)
		{
			return new Attribute("nr.apdexPerfZone", apdexPerfZone, false, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
		}

		[NotNull]
		public static Attribute BuildExternalDurationAttribute(Single durationInSec)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			return new Attribute("externalDuration", durationInSec, false, AttributeClassification.Intrinsics, destinations);
		}

		[NotNull]
		public static Attribute BuildExternalCallCountAttribute(Single count)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			return new Attribute("externalCallCount", count, false, AttributeClassification.Intrinsics, destinations);
		}

		[NotNull]
		public static Attribute BuildDatabaseDurationAttribute(Single durationInSec)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			return new Attribute("databaseDuration", durationInSec, false, AttributeClassification.Intrinsics, destinations);
		}

		[NotNull]
		public static Attribute BuildDatabaseCallCountAttribute(Single count)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorEvent | AttributeDestinations.TransactionEvent;
			return new Attribute("databaseCallCount", count, false, AttributeClassification.Intrinsics, destinations);
		}

		[NotNull]
		public static Attribute BuildErrorClassAttribute(String errorClass)
		{
			return new Attribute("error.class", errorClass, false, AttributeClassification.Intrinsics, AttributeDestinations.ErrorEvent);	
		}


		[NotNull]
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
		[NotNull]
		public static Attribute BuildCustomAttribute([NotNull] string key, [NotNull] object value)
		{
			key = TruncateUserProvidedValue(key);
			value = TruncateUserProvidedValue(value);
			return new Attribute(key, value, true, AttributeClassification.UserAttributes, AttributeDestinations.All);
		}

		[NotNull]
		public static Attribute BuildErrorDotMessageAttribute([NotNull] String errorMessage)
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
