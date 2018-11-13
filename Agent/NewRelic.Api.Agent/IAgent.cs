namespace NewRelic.Api.Agent
{
	/// <summary>
	/// Provides access to Agent artifacts and methods, such as the currently executing transaction.
	/// </summary>
	public interface IAgent
	{
		/// <summary>
		/// Property providing access to the currently executing transaction via the ITransaction interface.
		/// </summary>
		/// <example>
		/// <code>
		///   IAgent agent = GetAgent();
		///   ITransaction transaction = agent.CurrentTransaction;
		/// </code>
		/// </example>
		ITransaction CurrentTransaction { get; }
	}
}
