namespace NewRelic.Api.Agent
{
	/// <summary>
	/// 
	/// </summary>
	internal interface ITransaction
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="payload"></param>
		void AcceptDistributedTracePayload(string payload);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="payload"></param>
		void AcceptDistributedTracePayload(IDistributedTracePayload payload);

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		IDistributedTracePayload CreateDistributedTracePayload();
	}
}
