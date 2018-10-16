namespace NewRelic.Agent.Core
{
	public class AgentBridgeApi
	{
		public TransactionBridgeApi CurrentTransaction
		{
			get
			{
				return new TransactionBridgeApi();
			}
		}
	}
}