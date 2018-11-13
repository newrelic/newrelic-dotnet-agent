namespace NewRelic.Api.Agent
{
	/// <summary>
	/// Provides access to transaction-specific methods in the New Relic API.
	/// </summary>
	public interface ITransaction
	{
		/// <summary>
		/// Accepts an incoming Distributed Trace payload from another service.
		/// </summary>
		/// <param name="payload">A string representation of the incoming Distributed Trace payload.</param>
		/// <param name="transportType">An enum value describing the transport of the incoming payload (e.g. http). Default is TransportType.Unknown.</param>
		/// <example>
		/// <code>
		///   KeyValuePair&lt;string, string&gt; metadata;
		///   IAgent agent = GetAgent();
		///   ITransaction transaction = agent.CurrentTransaction;
		///   transaction.AcceptDistributedTracePayload(metadata.Value, TransportType.Queue);
		/// </code>
		/// </example>
		void AcceptDistributedTracePayload(string payload, TransportType transportType = TransportType.Unknown);

		/// <summary>
		/// Creates a Distributed Trace payload for inclusion in an outgoing request.
		/// </summary>
		/// <example>
		/// <code>
		///   IAgent agent = GetAgent();
		///   ITransaction transaction = agent.CurrentTransaction;
		///   IDistributedTracePayload payload = transaction.CreateDistributedTracePayload();
		/// </code>
		/// </example>
		/// <returns>Returns an object providing access to the outgoing payload.</returns>
		IDistributedTracePayload CreateDistributedTracePayload();
	}
}
