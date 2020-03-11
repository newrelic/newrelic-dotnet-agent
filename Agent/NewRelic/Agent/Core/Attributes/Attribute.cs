using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core;
using NewRelic.Core.CodeAttributes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NewRelic.Agent.Core.Attributes
{
	public abstract class Attribute
	{
		public const int CUSTOM_ATTRIBUTE_VALUE_LENGTH_CLAMP = 255;  //bytes
		private const int ATTRIBUTE_KEY_LENGTH_CLAMP = 255;             //bytes

		public string Key { get { return _key; } }
		private readonly string _key;

		public object Value => GetValue();
		protected abstract object GetValue();

		public AttributeDestinations DefaultDestinations { get { return _defaultDestinations; } }
		private readonly AttributeDestinations _defaultDestinations;

		public virtual AttributeClassification Classification { get; private set; }

		public bool IsValid => !IsInvalidKeyEmpty && !IsInvalidKeyTooLarge && !IsInvalidValueNull;

		public bool IsInvalidKeyEmpty { get; protected set; }
		public bool IsInvalidKeyTooLarge { get; protected set; }
		public bool IsInvalidValueNull { get; protected set; }
		public bool IsValueTruncated { get; protected set; }

		private Attribute(string key)
		{
			_key = key?.Trim();

			IsInvalidKeyEmpty = string.IsNullOrEmpty(_key);
			IsInvalidKeyTooLarge = !IsInvalidKeyEmpty && Encoding.UTF8.GetByteCount(_key) > ATTRIBUTE_KEY_LENGTH_CLAMP;
		}

		protected Attribute(string key, AttributeClassification classification, AttributeDestinations defaultDestinations) : this(key)
		{
			Classification = classification;
			_defaultDestinations = defaultDestinations;
		}

		protected Attribute(string key, AttributeDestinations defaultDestinations) : this(key)
		{
			_defaultDestinations = defaultDestinations;
		}

		#region "Attribute Builders"

		public static StringAttribute BuildQueueWaitTimeAttribute(TimeSpan queueTime)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent;

			var value = queueTime.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
			return new StringAttribute("queue_wait_time_ms", value, AttributeClassification.AgentAttributes, destinations);
		}

		public static DoubleAttribute BuildQueueDurationAttribute(TimeSpan queueTime)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			var value = queueTime.TotalSeconds;
			return new DoubleAttribute("queueDuration", value, AttributeClassification.Intrinsics, destinations);
		}

		public static StringAttribute BuildOriginalUrlAttribute(string value)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent;
			return new StringAttribute("original_url", value, AttributeClassification.AgentAttributes, destinations);
		}

		public static StringAttribute BuildRequestUriAttribute(string value)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.SqlTrace;
			return new StringAttribute("request.uri", value, AttributeClassification.AgentAttributes, destinations);
		}

		public static StringAttribute BuildRequestRefererAttribute(string value)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorEvent;
			return new StringAttribute("request.referer", value, AttributeClassification.AgentAttributes, destinations);
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
		public static StringAttribute BuildRequestParameterAttribute(string key, string value)
		{
			key = ("request.parameters." + key);
			return new StringAttribute(key, value, AttributeClassification.AgentAttributes, AttributeDestinations.None);
		}

		/// <summary>
		/// Deprecated: This attribute should be removed in a major release version >= 9.
		/// Usages of this method should be replaced by BuildHttpStatusCodeAttribute.
		/// </summary>
		/// <param name="value">The attribute value</param>
		/// <returns>The created attribute.</returns>
		[ToBeRemovedInFutureRelease("To be removed v9+. Use BuildHttpStatusCodeAttribute instead.")]
		public static StringAttribute BuildResponseStatusAttribute(string value)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			return new StringAttribute("response.status", value, AttributeClassification.AgentAttributes, destinations);
		}

		public static IntAttribute BuildHttpStatusCodeAttribute(int value)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent;
			return new IntAttribute("http.statusCode", value, AttributeClassification.AgentAttributes, destinations);
		}

		public static StringAttribute BuildClientCrossProcessIdAttribute(string value)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace;
			return new StringAttribute("client_cross_process_id", value, AttributeClassification.Intrinsics, destinations);
		}

		public static StringAttribute BuildTripUnderscoreIdAttribute(string value)
		{
			return new StringAttribute("trip_id", value, AttributeClassification.Intrinsics, AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace);
		}

		public static StringAttribute BuildCatNrTripIdAttribute(string value)
		{
			return new StringAttribute("nr.tripId", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
		}

		public static IEnumerable<Attribute> BuildBrowserTripIdAttribute(string value)
		{
			return new[]
			{
				new StringAttribute("nr.tripId", value, AttributeClassification.AgentAttributes, AttributeDestinations.JavaScriptAgent)
			};
		}

		public static IEnumerable<Attribute> BuildCatPathHash(string value)
		{
			return new[]
			{
				new StringAttribute("path_hash", value, AttributeClassification.Intrinsics, AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace),
				new StringAttribute("nr.pathHash", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent)
			};
		}

		public static IEnumerable<Attribute> BuildCatReferringPathHash(string value)
		{
			return new[]
			{
				new StringAttribute("nr.referringPathHash", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent)
			};
		}

		public static IEnumerable<Attribute> BuildCatReferringTransactionGuidAttribute(string value)
		{
			return new[]
			{
				new StringAttribute("referring_transaction_guid", value, AttributeClassification.Intrinsics, AttributeDestinations.ErrorTrace | AttributeDestinations.TransactionTrace),
				new StringAttribute("nr.referringTransactionGuid", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent)
			};
		}

		public static IEnumerable<Attribute> BuildCatAlternatePathHashes(string value)
		{
			return new[]
			{
				new StringAttribute("nr.alternatePathHashes", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent)
			};
		}

		public static StringAttribute BuildTransactionIdAttribute(string guid)
		{
			return new StringAttribute("transactionId", guid, AttributeClassification.Intrinsics, AttributeDestinations.SpanEvent);
		}

		public static StringAttribute BuildNameAttributeForSpanEvent(string name)
		{
			return new StringAttribute("name", name, AttributeClassification.Intrinsics, AttributeDestinations.SpanEvent);
		}

		public static StringAttribute BuildSpanCategoryAttribute(SpanCategory category)
		{
			return new StringAttribute("category", EnumNameCache<SpanCategory>.GetNameToLower(category), AttributeClassification.Intrinsics, AttributeDestinations.SpanEvent);
		}

		public static BoolAttribute BuildNrEntryPointAttribute(bool value)
		{
			return new BoolAttribute("nr.entryPoint", value, AttributeClassification.Intrinsics, AttributeDestinations.SpanEvent);
		}

		public static StringAttribute BuildComponentAttribute(string value)
		{
			return new StringAttribute("component", value, AttributeClassification.Intrinsics, AttributeDestinations.SpanEvent);
		}

		public static StringAttribute BuildSpanKindAttribute()
		{
			return new StringAttribute("span.kind", "client", AttributeClassification.Intrinsics, AttributeDestinations.SpanEvent);
		}

		public static DatastoreStatementAttribute BuildDbStatementAttribute(string value)
		{
			return new DatastoreStatementAttribute("db.statement", value, AttributeClassification.AgentAttributes, AttributeDestinations.SpanEvent);
		}

		public static StringAttribute BuildDbCollectionAttribute(string value)
		{
			return new StringAttribute("db.collection", value, AttributeClassification.AgentAttributes, AttributeDestinations.SpanEvent);
		}

		public static StringAttribute BuildDbInstanceAttribute(string value)
		{
			return new StringAttribute("db.instance", value, AttributeClassification.AgentAttributes, AttributeDestinations.SpanEvent);
		}

		public static StringAttribute BuildPeerAddress(string value)
		{
			return new StringAttribute("peer.address", value, AttributeClassification.AgentAttributes, AttributeDestinations.SpanEvent);
		}

		public static StringAttribute BuildPeerHostname(string value)
		{
			return new StringAttribute("peer.hostname", value, AttributeClassification.AgentAttributes, AttributeDestinations.SpanEvent);
		}

		public static StringAttribute BuildHttpUrlAttribute(string value)
		{
			return new StringAttribute("http.url", value, AttributeClassification.AgentAttributes, AttributeDestinations.SpanEvent);
		}

		public static StringAttribute BuildHttpMethodAttribute(string value)
		{
			return new StringAttribute("http.method", value, AttributeClassification.AgentAttributes, AttributeDestinations.SpanEvent);
		}

		public static Attribute BuildCustomAttributeForError(string key, object value)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace;
			return BuildCustomAttribute(key, value, AttributeClassification.UserAttributes, destinations);
		}

		public static Attribute BuildCustomAttributeForSpan(string key, object value)
		{
			const AttributeDestinations destinations = AttributeDestinations.SpanEvent;
			return BuildCustomAttribute(key, value, AttributeClassification.UserAttributes, destinations);
		}

		public static Attribute BuildCustomAttributeForCustomEvent(string key, object value)
		{
			const AttributeDestinations destinations = AttributeDestinations.CustomEvent;
			return BuildCustomAttribute(key, value, AttributeClassification.UserAttributes, destinations);
		}

		public static Attribute BuildCustomAttribute(string key, object value)
		{
			return BuildCustomAttribute(key, value, AttributeClassification.UserAttributes, AttributeDestinations.All);
		}

		public static StringAttribute BuildErrorTypeAttribute(string errorType)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent;
			return new StringAttribute("errorType", errorType, AttributeClassification.Intrinsics, destinations);
		}

		public static StringAttribute BuildErrorMessageAttribute(string errorMessage)
		{
			return new StringAttribute("errorMessage", errorMessage, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
		}

		public static BoolAttribute BuildErrorAttribute(bool isError)
		{
			return new BoolAttribute("error", isError, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
		}

		public static LongAttribute BuildTimestampAttribute(DateTime startTime)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.SpanEvent | AttributeDestinations.CustomEvent;
			return new LongAttribute("timestamp", startTime.ToUnixTimeMilliseconds(), AttributeClassification.Intrinsics, destinations);
		}

		public static LongAttribute BuildErrorTimeStampAttribute(DateTime errorTime)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorEvent;
			return new LongAttribute("timestamp", errorTime.ToUnixTimeMilliseconds(), AttributeClassification.Intrinsics, destinations);
		}

		public static IEnumerable<Attribute> BuildTransactionNameAttribute(string transactionName)
		{
			return new[]
			{
				new StringAttribute("name", transactionName, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent),
				new StringAttribute("transactionName", transactionName, AttributeClassification.Intrinsics, AttributeDestinations.ErrorEvent)
			};
		}

		public static StringAttribute BuildTransactionNameAttributeForCustomError(string name)
		{
			return new StringAttribute("transactionName", name, AttributeClassification.Intrinsics, AttributeDestinations.ErrorEvent);
		}

		public static StringAttribute BuildNrGuidAttribute(string guid)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			return new StringAttribute("nr.guid", guid, AttributeClassification.Intrinsics, destinations);
		}

		public static StringAttribute BuildGuidAttribute(string guid)
		{
			return new StringAttribute("guid", guid, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorTrace | AttributeDestinations.SqlTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent);
		}

		public static IEnumerable<Attribute> BuildSyntheticsResourceIdAttributes(string syntheticsResourceId)
		{
			return new[]
			{
				new StringAttribute("nr.syntheticsResourceId", syntheticsResourceId, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent),
				new StringAttribute("synthetics_resource_id", syntheticsResourceId, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace)
			};
		}

		public static IEnumerable<Attribute> BuildSyntheticsJobIdAttributes(string syntheticsJobId)
		{
			return new[]
			{
				new StringAttribute("nr.syntheticsJobId", syntheticsJobId, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent),
				new StringAttribute("synthetics_job_id", syntheticsJobId, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace)
			};
		}

		public static IEnumerable<Attribute> BuildSyntheticsMonitorIdAttributes(string syntheticsMonitorId)
		{
			return new[]
			{
				new StringAttribute("nr.syntheticsMonitorId", syntheticsMonitorId, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent),
				new StringAttribute("synthetics_monitor_id", syntheticsMonitorId, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace)
			};
		}

		public static DoubleAttribute BuildDurationAttribute(TimeSpan duration)
		{
			var value = duration.TotalSeconds;
			return new DoubleAttribute("duration", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent);
		}

		public static DoubleAttribute BuildWebDurationAttribute(TimeSpan webTransactionDuration)
		{
			var value = webTransactionDuration.TotalSeconds;
			return new DoubleAttribute("webDuration", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
		}

		public static DoubleAttribute BuildTotalTime(TimeSpan totalTime)
		{
			var value = totalTime.TotalSeconds;
			return new DoubleAttribute("totalTime", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.TransactionTrace);
		}

		public static DoubleAttribute BuildCpuTime(TimeSpan cpuTime)
		{
			var value = cpuTime.TotalSeconds;
			return new DoubleAttribute("cpuTime", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.TransactionTrace);
		}

		public static StringAttribute BuildApdexPerfZoneAttribute(string apdexPerfZone)
		{
			return new StringAttribute("nr.apdexPerfZone", apdexPerfZone, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
		}

		public static FloatAttribute BuildExternalDurationAttribute(float durationInSec)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			return new FloatAttribute("externalDuration", durationInSec, AttributeClassification.Intrinsics, destinations);
		}

		public static FloatAttribute BuildExternalCallCountAttribute(float count)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			return new FloatAttribute("externalCallCount", count, AttributeClassification.Intrinsics, destinations);
		}

		public static FloatAttribute BuildDatabaseDurationAttribute(float durationInSec)
		{
			const AttributeDestinations destinations = AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent;
			return new FloatAttribute("databaseDuration", durationInSec, AttributeClassification.Intrinsics, destinations);
		}

		public static FloatAttribute BuildDatabaseCallCountAttribute(float count)
		{
			const AttributeDestinations destinations = AttributeDestinations.ErrorEvent | AttributeDestinations.TransactionEvent;
			return new FloatAttribute("databaseCallCount", count, AttributeClassification.Intrinsics, destinations);
		}

		public static StringAttribute BuildErrorClassAttribute(string errorClass)
		{
			return new StringAttribute("error.class", errorClass, AttributeClassification.Intrinsics, AttributeDestinations.ErrorEvent);
		}

		public static StringAttribute BuildTypeAttribute(TypeAttributeValue typeAttribute)
		{
			var destinations = AttributeDestinations.None;

			if (typeAttribute == TypeAttributeValue.TransactionError)
				destinations |= AttributeDestinations.ErrorEvent;
			else if (typeAttribute == TypeAttributeValue.Transaction)
				destinations |= AttributeDestinations.TransactionEvent;
			else if (typeAttribute == TypeAttributeValue.Span)
				destinations |= AttributeDestinations.SpanEvent;


			return new StringAttribute("type", EnumNameCache<TypeAttributeValue>.GetName(typeAttribute), AttributeClassification.Intrinsics, destinations);
		}

		public static StringAttribute BuildErrorDotMessageAttribute(string errorMessage)
		{
			return new StringAttribute("error.message", errorMessage, AttributeClassification.Intrinsics, AttributeDestinations.ErrorEvent);
		}

		public static StringAttribute BuildParentTypeAttribute(string value)
		{
			return new StringAttribute("parent.type", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorTrace | AttributeDestinations.SqlTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent);
		}

		public static StringAttribute BuildParentAccountAttribute(string value)
		{
			return new StringAttribute("parent.account", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorTrace | AttributeDestinations.SqlTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent);
		}

		public static StringAttribute BuildParentAppAttribute(string value)
		{
			return new StringAttribute("parent.app", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorTrace | AttributeDestinations.SqlTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent);
		}

		public static StringAttribute BuildParentTransportTypeAttribute(string value)
		{
			return new StringAttribute("parent.transportType", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorTrace | AttributeDestinations.SqlTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent);
		}

		public static DoubleAttribute BuildParentTransportDurationAttribute(TimeSpan value)
		{
			var durationInSeconds = (float)value.TotalSeconds;
			return new DoubleAttribute("parent.transportDuration", durationInSeconds, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorTrace | AttributeDestinations.SqlTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent);
		}

		public static StringAttribute BuildParentSpanIdAttribute(string value)
		{
			return new StringAttribute("parentSpanId", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent);
		}

		public static StringAttribute BuildParentIdAttribute(string value)
		{
			return new StringAttribute("parentId", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionEvent | AttributeDestinations.SpanEvent);
		}

		public static StringAttribute BuildDistributedTraceIdAttributes(string value)
		{
			return new StringAttribute("traceId", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorTrace | AttributeDestinations.SqlTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent);
		}

		public static FloatAttribute BuildPriorityAttribute(float value)
		{
			return new FloatAttribute("priority", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorTrace | AttributeDestinations.SqlTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent);
		}

		public static BoolAttribute BuildSampledAttribute(bool value)
		{
			return new BoolAttribute("sampled", value, AttributeClassification.Intrinsics, AttributeDestinations.TransactionTrace | AttributeDestinations.ErrorTrace | AttributeDestinations.SqlTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent);
		}

		public static StringAttribute BuildHostDisplayNameAttribute(string value)
		{
			return new StringAttribute("host.displayName", value, AttributeClassification.AgentAttributes, AttributeDestinations.TransactionTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.ErrorEvent);
		}

		public static StringAttribute BuildCustomEventTypeAttribute(string value)
		{
			return new StringAttribute("type", value, AttributeClassification.Intrinsics, AttributeDestinations.CustomEvent);
		}

		#endregion "Attribute Builders"

		#region "Custom Attribute Builders Implemenations"

		private static Attribute BuildCustomAttribute(string key, object value, AttributeClassification classification, AttributeDestinations destinations)
		{
			//passing down NULL makes the attribute invalid.
			//probably a better way to do this.
			if (value == null)
			{
				return BuildCustomAttribute(key, null as string, classification, destinations);
			}

			if (value is TimeSpan)
			{
				value = ((TimeSpan)value).TotalSeconds;
			}
			else if (value is DateTimeOffset)
			{
				value = ((DateTimeOffset)value).ToString("o");
			}

			switch (Type.GetTypeCode(value.GetType()))
			{
				case TypeCode.SByte:
				case TypeCode.Byte:
				case TypeCode.UInt16:
				case TypeCode.UInt32:
				case TypeCode.UInt64:
				case TypeCode.Int16:
				case TypeCode.Int32:
				case TypeCode.Int64:
					return BuildCustomAttribute(key, Convert.ToInt64(value), classification, destinations);

				case TypeCode.Decimal:
				case TypeCode.Single:
				case TypeCode.Double:
					return BuildCustomAttribute(key, Convert.ToDouble(value), classification, destinations);

				case TypeCode.Boolean:
					return BuildCustomAttribute(key, Convert.ToBoolean(value), classification, destinations);

				case TypeCode.String:
					return BuildCustomAttribute(key, (string)value, classification, destinations);

				case TypeCode.DateTime:
					return BuildCustomAttribute(key, ((DateTime)value).ToString("o"), classification, destinations);

				default:
					return BuildCustomAttribute(key, value.ToString(), classification, destinations);
			}
		}

		private static StringAttribute BuildCustomAttribute(string key, string value, AttributeClassification classification, AttributeDestinations destinations)
		{
			return new StringAttribute(key, value, classification, destinations);
		}

		private static LongAttribute BuildCustomAttribute(string key, long value, AttributeClassification classification, AttributeDestinations destinations)
		{
			return new LongAttribute(key, value, classification, destinations);
		}

		private static DoubleAttribute BuildCustomAttribute(string key, double value, AttributeClassification classification, AttributeDestinations destinations)
		{
			return new DoubleAttribute(key, value, classification, destinations);
		}

		private static BoolAttribute BuildCustomAttribute(string key, bool value, AttributeClassification classification, AttributeDestinations destinations)
		{
			return new BoolAttribute(key, value, classification, destinations);
		}

		#endregion

	}

	public abstract class Attribute<TValue> : Attribute
	{
		private readonly TValue _value;
		public new TValue Value => _value;
		protected override object GetValue() => _value;

		protected Attribute(string key, TValue value, AttributeClassification classification, AttributeDestinations defaultDestinations)
			: base(key, classification, defaultDestinations)
		{
			_value = value;
			IsInvalidValueNull = _value == null;
		}
	}

	public static class IAttributeExtensions
	{
		public static bool HasDestination(this Attribute attribute, AttributeDestinations destination)
		{
			if (attribute == null)
				return false;

			return (attribute.DefaultDestinations & destination) == destination;
		}
	}
}
