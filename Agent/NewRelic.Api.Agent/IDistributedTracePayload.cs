namespace NewRelic.Api.Agent
{
	/// <summary>
	/// 
	/// </summary>
	internal interface IDistributedTracePayload
	{
		/// <summary>
		/// 
		/// </summary>
		string HttpSafe { get; }

		/// <summary>
		/// 
		/// </summary>
		string Text { get; }
	}
}
