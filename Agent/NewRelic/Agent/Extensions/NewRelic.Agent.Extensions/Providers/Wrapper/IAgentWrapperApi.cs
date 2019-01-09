using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{

	/// <summary>
	/// The API that the agent provides to wrappers.
	/// </summary>
	public interface IAgentWrapperApi
	{
		IConfiguration Configuration { get; }

		/// <summary>
		/// Returns the current transaction.  This will either return a transaction
		/// if one has already been started or a dummy instance of a transaction
		/// if one does not already exist.
		/// </summary>
		[NotNull]
		ITransactionWrapperApi CurrentTransactionWrapperApi { get; }

		/// <summary>
		/// Create a new transaction for processing a web request.
		/// </summary>
		/// <param name="type">The type of web transaction.</param>
		/// <param name="name">The name of the transaction. Must not be null.</param>
		/// <param name="mustBeRootTransaction">Whether or not the transaction must be root.</param>
		/// <param name="onCreate">A callback that is called if a transaction is created. Can be null.</param>
		/// <exception cref="System.ArgumentNullException"></exception>
		ITransactionWrapperApi CreateWebTransaction(WebTransactionType type, [NotNull] string name, bool mustBeRootTransaction = true, Action onCreate = null);

		/// <summary>
		/// Create a new transaction for processing a message received from a message queue.
		/// </summary>
		/// <param name="destinationType"></param>
		/// <param name="brokerVendorName">The name of the message broker vendor. Must not be null.</param>
		/// <param name="destination">The destination queue of the message being handled. Can be null.</param>
		/// <param name="onCreate">A callback that is called if a transaction is created. Can be null.</param>
		/// <exception cref="System.ArgumentNullException"></exception>
		ITransactionWrapperApi CreateMessageBrokerTransaction(MessageBrokerDestinationType destinationType, [NotNull] string brokerVendorName, [CanBeNull] string destination = null, Action onCreate = null);

		/// <summary>
		/// Create a new transaction for processing an arbitrary transaction.
		/// </summary>
		/// <param name="category">The general category of the transaction. Must not be null.</param>
		/// <param name="name">The name of the transaction. Must not be null.</param>
		/// <param name="mustBeRootTransaction">Whether or not the transaction can exist within another transaction</param>
		/// <param name="onCreate">A callback that is called if a transaction is created. Can be null.</param>
		/// <exception cref="System.ArgumentNullException"></exception>
		ITransactionWrapperApi CreateOtherTransaction([NotNull] string category, [NotNull] string name, bool mustBeRootTransaction = true, Action onCreate = null);

		/// <summary>
		/// Casts an object as an ISegment instance.  This should be used when casting values retrieved from 
		/// dictionaries as segments because it guarantees a non-null segment return value.
		/// </summary>
		/// <param name="segment">An object that should be an instance of ISegment</param>
		/// <returns>A non-null ISegment instance.</returns>
		[NotNull]
		ISegment CastAsSegment(object segment);

		/// <summary>
		/// Sets up the resources necessary to execute an explain plan.
		/// </summary>
		/// <param name="segment">The datastore segment candidate for an explain plan</param>
		/// <param name="allocateExplainPlanResources">Function which returns the resources necessary for executing the explain plan</param>
		/// <param name="generateExplainPlan">Function for executing the explain plan</param>
		/// <param name="vendorValidateShouldExplain">Function for executing any additional vendor validation on if an explain plan should be ran</param>
		void EnableExplainPlans(ISegment segment, Func<object> allocateExplainPlanResources, Func<object, ExplainPlan> generateExplainPlan, Func<VendorExplainValidationResult> vendorValidateShouldExplain);

		/// <summary>
		/// Processes headers from an inbound request.
		/// </summary>
		/// <param name="headers">The headers to be processed. Must not be null.</param>
		/// <param name="contentLength">The length of the content, in bytes, if available.</param>
		/// <exception cref="System.ArgumentNullException"></exception>
		void ProcessInboundRequest([NotNull] IEnumerable<KeyValuePair<string, string>> headers, [CanBeNull] TransportType transportType, long? contentLength = null);

		/// <summary>
		/// Tell the agent about an error that just occurred in the wrapper. Normally exceptions should just be thrown so that the agent can handle them directly, but this method is useful in situations where exceptions are happening outside the scope of the agent (for example, on another thread). This method is thread-safe.
		/// </summary>
		/// <param name="exception">The exception that occurred. Must not be null.</param>
		void HandleWrapperException([NotNull] Exception exception);

		/// <summary>
		/// Returns a stream that will inject content that the agent thinks is important into <paramref name="stream"/>, or null.
		/// 
		/// This method should be called as late as possible (i.e. just before the stream is read) to ensure that the metadata passed in (encoding, contentType, etc) is no longer volatile.
		/// 
		/// This method will return null under many different conditions, including due to configuration settings or internal business logic.
		/// </summary>
		/// <param name="stream">The stream to inject content into.</param>
		/// <param name="encoding">The encoding of the data in the stream.</param>
		/// <param name="contentType">The type of content in the stream.</param>
		/// <param name="requestPath">The path of the request</param>
		[CanBeNull]
		Stream TryGetStreamInjector([CanBeNull] Stream stream, [CanBeNull] Encoding encoding, [CanBeNull] string contentType, [CanBeNull] string requestPath);

		bool TryGetDistributedTracePayloadFromHeaders<T>(IEnumerable<KeyValuePair<string, T>> headers, out T payload) where T : class;
	}

	public interface ISegment {

		/// <summary>
		/// Returns true if this is a real (not a no op) segment.
		/// </summary>
		/// <returns></returns>
		bool IsValid { get; }

		/// <summary>
		/// Returns true if the duration of this segment should not count towards the parent's exclusive time when async.
		/// </summary>
		bool DurationShouldBeDeductedFromParent { get; set; }

		/// <summary>
		/// Returns true if this is a leaf segment.
		/// </summary>
		bool IsLeaf { get; }

		/// <summary>
		/// Returns the current segment's Span Id.
		/// </summary>
		string SpanId { get; }

		/// <summary>
		/// Ends this transaction segment.
		/// </summary>
		void End();

		/// <summary>
		/// Ends this transaction segment in the exception case.
		/// </summary>
		void End(Exception ex);

		/// <summary>
		/// Marks this segment as combinable, which means that identical adjacent siblings that are also combinable will be combined into one segment. This is useful for segments that are tracking methods like `SqlDataReader.Read` which often gets called many times back to back, and where it is typically more interesting to see the segments aggregated together rather than separate.
		/// </summary>
		void MakeCombinable();

		/// <summary>
		/// Removes this segment from the top of the agent's internal call stack. Should only be used for asynchronous methods. Calling EndSegment is sufficient for synchronous methods.
		/// </summary>
		void RemoveSegmentFromCallStack();
	}
}
