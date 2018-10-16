namespace NewRelic.Api.Agent
{
	internal class NoOpAgent : IAgent
	{
		private static ITransaction _noOpTransaction = new NoOpTransaction();
		public ITransaction CurrentTransaction => _noOpTransaction;
	}
}
