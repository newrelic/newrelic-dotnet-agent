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

		private static bool _isAcceptDistributedTracePayloadOverload1Available = true;
		public void AcceptDistributedTracePayload(string payload)
		{
			if (!_isAcceptDistributedTracePayloadOverload1Available) return;

			try
			{
				_wrappedTransaction.AcceptDistributedTracePayload(payload);
			}
			catch (RuntimeBinderException)
			{
				_isAcceptDistributedTracePayloadOverload1Available = false;
			}
		}

		private static bool _isAcceptDistributedTracePayloadOverload2Available = true;
		public void AcceptDistributedTracePayload(IDistributedTracePayload payload)
		{
			if (!_isAcceptDistributedTracePayloadOverload2Available) return;

			try
			{
				_wrappedTransaction.AcceptDistributedTracePayload(payload);
			}
			catch (RuntimeBinderException)
			{
				_isAcceptDistributedTracePayloadOverload2Available = false;
			}
		}

		private static bool _isCreateDistributedTracePayloadAvailable = true;
		public IDistributedTracePayload CreateDistributedTracePayload()
		{
			if (!_isCreateDistributedTracePayloadAvailable) return _noOpTransaction.CreateDistributedTracePayload();

			try
			{
				var result = _wrappedTransaction.CreateDistributedTracePayload();
				return new DistributedTracePayload(result);
			}
			catch (RuntimeBinderException)
			{
				_isCreateDistributedTracePayloadAvailable = false;
			}

			return _noOpTransaction.CreateDistributedTracePayload();
		}
	}
}
