using System.Collections.Generic;
using System.Text;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core;
using NewRelic.Parsing;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	public interface ISpanEventMaker
	{
		IEnumerable<SpanEventWireModel> GetSpanEvents(ImmutableTransaction immutableTransaction, string transactionName);
	}

	public class SpanEventMaker: ISpanEventMaker
	{
		private const string GenericCategory = "generic";
		private const string DatastoreCategory = "datastore";
		private const string HttpCategory = "http";  //external
		private const string KeySpanKind = "span.kind";
		private const string ValueSpanKind = "client";
		private const string KeyComponent = "component";

		private const string KeyType = "type";
		private const string ValueType = "Span";
		private const string KeyTraceId = "traceId";
		private const string KeyGuid = "guid";
		private const string KeyParentId = "parentId";
		private const string KeyTransactionId = "transactionId";
		private const string KeySampled = "sampled";
		private const string KeyPriority = "priority";
		private const string KeyTimestamp = "timestamp";
		private const string KeyDuration = "duration";
		private const string KeyName = "name";
		private const string KeyCategory = "category";
		private const string KeyNrEntryPoint = "nr.entryPoint";

		/// <summary>
		/// Creates a series of SpanEventWireModel's from the current transactions Segments.
		/// </summary>
		/// <param name="immutableTransaction">Current transaction whose segments are to be processed into Span Events.</param>
		/// <param name="transactionName">The prefixed name of the current transaction.</param>
		/// <returns>A list of SpanEventWireModel's.</returns>
		public IEnumerable<SpanEventWireModel> GetSpanEvents(ImmutableTransaction immutableTransaction, string transactionName)
		{
			var spanEvents = new List<SpanEventWireModel>();

			// Create Root SpanEvent since we don't have a single root segment (we do similar for traces)
			var rootSpanId = GuidGenerator.GenerateNewRelicGuid();
			spanEvents.Add(GetFauxRootSpanEvent(immutableTransaction, transactionName, rootSpanId));

			foreach (var segment in immutableTransaction.Segments)
			{
				spanEvents.Add(GetSpanEventWireModel(immutableTransaction, segment, rootSpanId));
			}

			return spanEvents;
		}

		/// <summary>
		/// Creates a single root span, much like we do for Transaction Traces, since DT requires that there be only one parent-less span per txn (or at least the UI/Backend is expecting that). 
		/// </summary>
		/// <param name="immutableTransaction">Current transaction whose segments are being processed into Span Events.</param>
		/// /// <param name="transactionName">The prefixed name of the current transaction.</param>
		/// <param name="rootSpanId">SpanId of the faux root segment.</param>
		/// <returns>Returns completed SpanEventWireModel.</returns>
		private static SpanEventWireModel GetFauxRootSpanEvent(ImmutableTransaction immutableTransaction, string transactionName, string rootSpanId)
		{
			var spanAttributes = new Dictionary<string, object>();
			spanAttributes.Add(KeyType, ValueType);
			spanAttributes.Add(KeyTraceId, GetTraceId(immutableTransaction));
			spanAttributes.Add(KeyGuid, rootSpanId);
			// parentId should be null if very first span in trace, but should use DistributedTraceGuid otherwise.
			if (immutableTransaction.TransactionMetadata.HasIncomingDistributedTracePayload &&
				immutableTransaction.TransactionMetadata.DistributedTraceGuid != null)
			{
				spanAttributes.Add(KeyParentId, immutableTransaction.TransactionMetadata.DistributedTraceGuid);
			}

			spanAttributes.Add(KeyTransactionId, immutableTransaction.Guid);
			spanAttributes.Add(KeySampled, immutableTransaction.TransactionMetadata.DistributedTraceSampled);
			spanAttributes.Add(KeyPriority, immutableTransaction.TransactionMetadata.Priority);
			spanAttributes.Add(KeyTimestamp, immutableTransaction.StartTime.ToUnixTimeMilliseconds());
			spanAttributes.Add(KeyDuration, immutableTransaction.Duration.TotalSeconds);
			spanAttributes.Add(KeyName, transactionName);
			spanAttributes.Add(KeyCategory, GenericCategory);
			spanAttributes.Add(KeyNrEntryPoint, true);

			return new SpanEventWireModel(spanAttributes);
		}

		/// <summary>
		/// Creates a Span Event from a segment.
		/// </summary>
		/// <param name="immutableTransaction">Current transaction whose segments are being processed into Span Events.</param>
		/// <param name="segment">Current segment being processed into a Span Event.</param>
		/// <param name="rootSpanId">The SpanId of the faux root span.</param>
		/// <returns>Returns completed SpanEventWireModel.</returns>
		private static SpanEventWireModel GetSpanEventWireModel(ImmutableTransaction immutableTransaction, Segment segment, string rootSpanId)
		{
			var spanAttributes = new Dictionary<string, object>();

			spanAttributes.Add(KeyType, ValueType);

			// Per spec we want to use the traceId from the payload and fallback on something else if missing.  
			// Since we already use the transaction guid for this in payload creation it makes sense to do the same here.
			spanAttributes.Add(KeyTraceId, GetTraceId(immutableTransaction));

			// spanId of current span
			spanAttributes.Add(KeyGuid, segment.SpanId);

			// parentId, Should be either the spanId of the parent segment OR the spanId of the faux root span in the case the current segment has a null ParentUniqueId.
			spanAttributes.Add(KeyParentId, GetParentSpanId(segment, immutableTransaction, rootSpanId));

			// transactionId
			spanAttributes.Add(KeyTransactionId, immutableTransaction.Guid);

			// sampled, same type as txn
			spanAttributes.Add(KeySampled, immutableTransaction.TransactionMetadata.DistributedTraceSampled);

			// priority, same type as txn
			spanAttributes.Add(KeyPriority, immutableTransaction.TransactionMetadata.Priority);

			// timestamp, based on transaction start and adds segment start.
			spanAttributes.Add(KeyTimestamp, immutableTransaction.StartTime.Add(segment.RelativeStartTime).ToUnixTimeMilliseconds());

			//duration, segment duration. Can be null, but should never be.
			spanAttributes.Add(KeyDuration, (float)segment.DurationOrZero.TotalSeconds);

			// name, segment name
			spanAttributes.Add(KeyName, segment.GetTransactionTraceName());

			// category, created based on segmentdata type (*SegmentData types under Builders)
			var category = GetCategory(segment);
			spanAttributes.Add(KeyCategory, category);

			// Datastore specific attributes
			if (category == DatastoreCategory)
			{
				SetDatastoreAttributes(immutableTransaction, segment, spanAttributes);
			}

			// Http (External) specific attributes
			if (category == HttpCategory)
			{
				SetHttpAttributes(segment, spanAttributes);
			}

			return new SpanEventWireModel(spanAttributes);
		}

		/// <summary>
		/// Adds the Datastore specific attributes to the Span Event currently being created.
		/// </summary>
		/// <param name="immutableTransaction">Current transaction whose segments are being processed into Span Events.</param>
		/// <param name="segment">Current segment being processed into a Span Event.</param>
		/// <param name="spanAttributes">Dictionary to add new attributes to that already contains the generic Span attributes.</param>
		private static void SetDatastoreAttributes(ImmutableTransaction immutableTransaction, Segment segment, Dictionary<string, object> spanAttributes)
		{
			const string keyDbStatement = "db.statement";
			const string keyDbInstance = "db.instance";
			const string keyPeerAddress = "peer.address";
			const string keyPeerHostname = "peer.hostname";
			
			var data = (DatastoreSegmentData)segment.Data;
			spanAttributes.Add(KeyComponent, EnumNameCache<DatastoreVendor>.GetName(data.DatastoreVendorName));

			if (!string.IsNullOrWhiteSpace(data.CommandText))
			{
				var statement = immutableTransaction.GetSqlObfuscatedAccordingToConfig(data.CommandText, data.DatastoreVendorName);
				spanAttributes.Add(keyDbStatement, TruncateDatastoreStatement(statement));
			}

			spanAttributes.Add(keyDbInstance, data.DatabaseName);
			spanAttributes.Add(keyPeerAddress, $"{data.Host}:{data.PortPathOrId}");
			spanAttributes.Add(keyPeerHostname, data.Host);
			spanAttributes.Add(KeySpanKind, ValueSpanKind);
		}

		/// <summary>
		/// Adds the Http (external) specific attributes to the Span Event currently being created.
		/// </summary>
		/// <param name="segment">Current segment being processed into a Span Event.</param>
		/// <param name="spanAttributes">Dictionary to add new attributes to that already contains the generic Span attributes.</param>
		private static void SetHttpAttributes(Segment segment, Dictionary<string, object> spanAttributes)
		{
			const string keyHttpUrl = "http.url";
			const string keyHttpMethod = "http.method";

			var data = (ExternalSegmentData)segment.Data;
			spanAttributes.Add(keyHttpUrl, StringsHelper.CleanUri(data.Uri));
			spanAttributes.Add(keyHttpMethod, data.Method);
			spanAttributes.Add(KeyComponent, segment.MethodCallData.TypeName);
			spanAttributes.Add(KeySpanKind, ValueSpanKind);
		}

		/// <summary>
		/// Truncates the statement from a Datastore segment to 2000 bytes or less.  This occurs post-obfuscation.
		/// </summary>
		/// <param name="statement">Obfuscated statement from datastore segment.</param>
		/// <returns>Truncated statement.</returns>
		private static string TruncateDatastoreStatement(string statement)
		{
			const int totalMaxStatementLength = 2000;
			const int maxBytesPerUtf8Char = 4;
			const int maxCharactersWillFitWithoutTruncation = totalMaxStatementLength / maxBytesPerUtf8Char;
			const int actualMaxStatementLength = 1997;
			const byte firstByte = 0b11000000;
			const byte highBit = 0b10000000;

			if (statement.Length <= maxCharactersWillFitWithoutTruncation)
			{
				return statement;
			}

			var byteArray = Encoding.UTF8.GetBytes(statement);

			if (byteArray.Length <= totalMaxStatementLength)
			{
				return statement;
			}

			var byteOffset = actualMaxStatementLength;
			
			// Check high bit to see if we're [potentially] in the middle of a multi-byte char
			if ((byteArray[byteOffset] & highBit) == highBit)
			{
				// If so, keep walking back until we have a byte starting with `11`,
				// which means the first byte of a multi-byte UTF8 character.
				while (firstByte != (byteArray[byteOffset] & firstByte))
				{
					byteOffset--;
				}
			}

			return Encoding.UTF8.GetString(byteArray, 0, byteOffset) + "...";
		}

		/// <summary>
		/// Uses the segment type to determine what category a span should be.
		/// </summary>
		/// <param name="segment">Current segment being processed into a Span Event.</param>
		/// <returns>Category span belongs to.</returns>
		private static string GetCategory(Segment segment)
		{
			const string datastoreSegmentData = "DatastoreSegmentData";
			const string externalSegmentData = "ExternalSegmentData";
			var segmentType = segment.Data.GetType().Name;

			if (segmentType == datastoreSegmentData)
			{
				return DatastoreCategory;
			}

			if (segmentType == externalSegmentData)
			{
				return HttpCategory;
			}

			return GenericCategory;
		}

		/// <summary>
		/// Determines the parentId (SpanId) for the span being created.
		/// </summary>
		/// <param name="segment">Current segment being processed into a Span Event.</param>
		/// <param name="immutableTransaction">Current transaction whose segments are being processed into Span Events.</param>
		/// <param name="rootSpanId">SpanId of the faux root segment.</param>
		/// <returns>SpanId of the parent segment.</returns>
		private static string GetParentSpanId(Segment segment, ImmutableTransaction immutableTransaction, string rootSpanId)
		{
			if (segment.ParentUniqueId == null)
			{
				return rootSpanId;
			}

			foreach (var otherSegment in immutableTransaction.Segments)
			{
				if (otherSegment.UniqueId != segment.ParentUniqueId)
				{
					continue;
				}

				return otherSegment.SpanId;
			}

			return rootSpanId;
		}

		private static string GetTraceId(ImmutableTransaction immutableTransaction)
		{
			return immutableTransaction.TransactionMetadata.DistributedTraceTraceId ?? immutableTransaction.Guid;
		}
	}
}