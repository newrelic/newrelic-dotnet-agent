using NewRelic.Agent.Extensions.Providers;
using System;
using System.Runtime.CompilerServices;

namespace NewRelic.Providers.Storage.OperationContext
{
	public class OperationContextStorageFactory : IContextStorageFactory
	{
		public bool IsAsyncStorage => false;

		bool IContextStorageFactory.IsValid
		{
			get
			{
				// if attempting to access OperationContext throws an exception then this factory is invalid
				try
				{
					AccessOperationContext();
				}
				catch (Exception)
				{
					return false;
				}
				return true;
			}
		}

		ContextStorageType IContextStorageFactory.Type => ContextStorageType.OperationContext;

		IContextStorage<T> IContextStorageFactory.CreateContext<T>(String key)
		{
			return new OperationContextStorage<T>(key);
		}

		[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
		private void AccessOperationContext()
		{
			// we want to force access to the operation context so we can get an type load exception if WCF is not available
			var temp = System.ServiceModel.OperationContext.Current;
		}
	}
}
