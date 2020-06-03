#if NETSTANDARD2_0
using System;

namespace NewRelic.SystemInterfaces.Web
{

	public class HttpRuntimeStatic : IHttpRuntimeStatic
	{
		public string AppDomainAppVirtualPath => string.Empty; //Microsoft.AspNetCore.Http.AppDomainAppVirtualPath;
	}
}
#endif
