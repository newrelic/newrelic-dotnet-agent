using Microsoft.CSharp.RuntimeBinder;

namespace NewRelic.Api.Agent
{
	internal class Transaction : ITransaction
	{
		private readonly dynamic _wrappedTransaction;
		private static ITransaction _noOpTransaction = new NoOpTransaction();

		internal Transaction(dynamic wrappedTransaction = null)
		{
			_wrappedTransaction = wrappedTransaction ?? _noOpTransaction;
		}

		private static bool _isAcceptDistributedTracePayloadAvailable = true;
		public void AcceptDistributedTracePayload(string payload, TransportType transportType = TransportType.Unknown)
		{
			if (!_isAcceptDistributedTracePayloadAvailable) return;

			try
			{
				_wrappedTransaction.AcceptDistributedTracePayload(payload, (int)transportType);
			}
			catch (RuntimeBinderException)
			{
				_isAcceptDistributedTracePayloadAvailable = false;
			}
		}

		private static bool _isCreateDistributedTracePayloadAvailable = true;
		public IDistributedTracePayload CreateDistributedTracePayload()
		{
			if (!_isCreateDistributedTracePayloadAvailable) return _noOpTransaction.CreateDistributedTracePayload();

			try
			{
				var result = _wrappedTransaction.CreateDistributedTracePayload();
				if (result != null)
				{
					return new DistributedTracePayload(result);
				}
			}
			catch (RuntimeBinderException)
			{
				_isCreateDistributedTracePayloadAvailable = false;
			}

			return _noOpTransaction.CreateDistributedTracePayload();
		}
	}
}
