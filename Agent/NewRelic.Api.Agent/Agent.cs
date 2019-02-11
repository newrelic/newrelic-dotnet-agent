using System.Runtime.CompilerServices;
using Microsoft.CSharp.RuntimeBinder;

namespace NewRelic.Api.Agent
{
	internal class Agent : IAgent
	{
		private static IAgent _noOpAgent = new NoOpAgent();
		private dynamic _wrappedAgent = _noOpAgent;

		[MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
		internal void SetWrappedAgent(object agentBridge)
		{
			_wrappedAgent = agentBridge;
		}

		private static bool _isCurrentTransactionAvailable = true;
		public ITransaction CurrentTransaction
		{
			get
			{
				if (!_isCurrentTransactionAvailable) return _noOpAgent.CurrentTransaction;

				try
				{
					var wrappedTransaction = _wrappedAgent.CurrentTransaction;
					if (wrappedTransaction != null)
					{
						return new Transaction(wrappedTransaction);
					}
				}
				catch (RuntimeBinderException)
				{
					_isCurrentTransactionAvailable = false;
				}

				return _noOpAgent.CurrentTransaction;
			}
		}
	}
}
