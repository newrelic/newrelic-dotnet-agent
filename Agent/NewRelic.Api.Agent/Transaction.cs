using Microsoft.CSharp.RuntimeBinder;
using System;

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

		//[Obsolete("AcceptDistributedTracePayload is deprecated.")]
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

		//[Obsolete("CreateDistributedTracePayload is deprecated.")]
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

		private static bool _isAddCustomAttributeAvailable = true;
		public ITransaction AddCustomAttribute(string key, object value)
		{

			if(!_isAddCustomAttributeAvailable)
			{
				return _noOpTransaction.AddCustomAttribute(key, value);
			}

			try
			{
				return _wrappedTransaction.AddCustomAttribute(key, value);
			}
			catch (RuntimeBinderException)
			{
				_isAddCustomAttributeAvailable = false;
			}

			return _noOpTransaction;
		}
	}
}
