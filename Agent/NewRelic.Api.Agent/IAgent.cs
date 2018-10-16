namespace NewRelic.Api.Agent
{
	/// <summary>
	/// 
	/// </summary>
	internal interface IAgent
	{
		/// <summary>
		/// 
		/// </summary>
		ITransaction CurrentTransaction { get; }
	}
}
