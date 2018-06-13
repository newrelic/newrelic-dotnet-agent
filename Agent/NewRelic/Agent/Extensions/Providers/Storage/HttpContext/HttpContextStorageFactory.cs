using NewRelic.Agent.Extensions.Providers;
using System;
using System.Runtime.CompilerServices;

namespace NewRelic.Providers.Storage.HttpContext
{
	public class HttpContextStorageFactory : IContextStorageFactory
	{
		public bool IsAsyncStorage => false;

		bool IContextStorageFactory.IsValid
		{
			get
			{
				// if attempting to access HttpContext throws an exception then this factory is invalid
				try
				{
					AccessHttpContextClass();
				}
				catch (Exception)
				{
					return false;
				}
				return true;
			}
		}

		ContextStorageType IContextStorageFactory.Type => ContextStorageType.HttpContext;

		IContextStorage<T> IContextStorageFactory.CreateContext<T>(String key)
		{
			return new HttpContextStorage<T>(key);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void AccessHttpContextClass()
		{
			if (System.Web.HttpContext.Current == null)
				return;
		}
	}
}
