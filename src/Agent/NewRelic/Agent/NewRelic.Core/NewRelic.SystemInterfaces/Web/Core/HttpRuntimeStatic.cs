#if NETSTANDARD2_0
using System;

namespace NewRelic.SystemInterfaces.Web
{

	public class HttpRuntimeStatic : IHttpRuntimeStatic
	{
		//TODO: What is the Microsoft.AspNetCore.Http equiv
		public string AppDomainAppVirtualPath => string.Empty; //Microsoft.AspNetCore.Http.AppDomainAppVirtualPath;
	}
}
#endif